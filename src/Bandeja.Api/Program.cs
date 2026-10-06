using System.Text;
using Bandeja.Api;
using Bandeja.Application;
using Bandeja.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddDbContext<InboxDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Inbox") ??
        "Host=postgres;Port=5432;Database=inbox;Username=inbox;Password=inbox"));
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<InboxService>();
builder.Services.AddSingleton<IMessagePublisher, RabbitMessageBus>();
builder.Services.AddSingleton<IInboxNotifier, SignalRInboxNotifier>();
builder.Services.AddHttpClient<ITelegramGateway, TelegramGateway>();
builder.Services.AddHostedService<RabbitMessageConsumer>();
builder.Services.AddHostedService<TelegramWebhookRegistrar>();

var app = builder.Build();

app.UseCors();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHub<InboxHub>("/hubs/inbox");
app.MapGet("/api/conversations", async (InboxService inbox, CancellationToken cancellationToken) =>
    Results.Ok(await inbox.GetConversationsAsync(cancellationToken)));
app.MapGet("/api/conversations/{chatId:long}/messages", async (long chatId, InboxService inbox, CancellationToken cancellationToken) =>
    Results.Ok(await inbox.GetMessagesAsync(chatId, cancellationToken)));
app.MapPost("/api/conversations/{chatId:long}/read", async (long chatId, InboxService inbox, CancellationToken cancellationToken) =>
{
    await inbox.MarkReadAsync(chatId, cancellationToken);
    return Results.NoContent();
});
app.MapPost("/api/conversations/{chatId:long}/messages", async (long chatId, ReplyRequest request, InboxService inbox, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await inbox.ReplyAsync(chatId, request.Text, cancellationToken));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
app.MapPost("/api/telegram/webhook", async (HttpRequest request, IConfiguration configuration, IMessagePublisher publisher, CancellationToken cancellationToken) =>
{
    var expectedSecret = configuration["Telegram:WebhookSecret"];
    var receivedSecret = request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
    if (string.IsNullOrWhiteSpace(expectedSecret) || !SecretMatches(expectedSecret, receivedSecret))
        return Results.Unauthorized();

    var update = await request.ReadFromJsonAsync<TelegramUpdate>(cancellationToken);
    if (update?.Message?.Text is not { Length: > 0 } text || text.Length > 4096)
        return Results.Ok();

    var message = update.Message;
    var sender = message.From;
    var displayName = string.Join(' ', new[] { sender?.FirstName ?? message.Chat.FirstName, sender?.LastName ?? message.Chat.LastName }
        .Where(name => !string.IsNullOrWhiteSpace(name)));
    await publisher.PublishAsync(new IncomingTelegramMessage(
        message.Chat.Id,
        message.MessageId,
        string.IsNullOrWhiteSpace(displayName) ? message.Chat.Username ?? message.Chat.Id.ToString() : displayName,
        sender?.Username ?? message.Chat.Username,
        text,
        DateTimeOffset.FromUnixTimeSeconds(message.Date)), cancellationToken);
    return Results.Accepted();
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();

static bool SecretMatches(string expected, string received)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var receivedBytes = Encoding.UTF8.GetBytes(received);
    return expectedBytes.Length == receivedBytes.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
}

public sealed record ReplyRequest(string Text);
