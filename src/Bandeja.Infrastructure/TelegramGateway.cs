using System.Net.Http.Json;
using System.Text.Json;
using Bandeja.Application;
using Microsoft.Extensions.Configuration;

namespace Bandeja.Infrastructure;

public sealed class TelegramGateway(HttpClient httpClient, IConfiguration configuration) : ITelegramGateway
{
    public async Task<long?> SendTextAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var token = configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Falta configurar el token del bot de Telegram.");

        var response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/sendMessage",
            new { chat_id = chatId, text },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        return payload?.RootElement.GetProperty("result").GetProperty("message_id").GetInt64();
    }
}
