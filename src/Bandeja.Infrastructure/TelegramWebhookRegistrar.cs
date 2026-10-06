using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bandeja.Infrastructure;

public sealed class TelegramWebhookRegistrar(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<TelegramWebhookRegistrar> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = configuration["Telegram:BotToken"];
        var publicUrl = configuration["Telegram:PublicBaseUrl"]?.TrimEnd('/');
        var secret = configuration["Telegram:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(publicUrl) || string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Webhook registration skipped: Telegram token, public base URL, or webhook secret is missing");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await clients.CreateClient().PostAsJsonAsync(
                    $"https://api.telegram.org/bot{token}/setWebhook",
                    new { url = $"{publicUrl}/api/telegram/webhook", secret_token = secret, allowed_updates = new[] { "message" } },
                    stoppingToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TelegramApiResult>(cancellationToken: stoppingToken);
                if (result?.Ok != true)
                    throw new InvalidOperationException(result?.Description ?? "Telegram rejected the webhook registration");
                logger.LogInformation("Telegram webhook registered at {WebhookUrl}", $"{publicUrl}/api/telegram/webhook");
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Webhook registration failed; retrying in 10 seconds");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private sealed record TelegramApiResult(bool Ok, string? Description);
}
