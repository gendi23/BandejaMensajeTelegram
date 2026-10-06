using System.Text.Json.Serialization;

namespace Bandeja.Api;

public sealed record TelegramUpdate([property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("date")] long Date,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("from")] TelegramUser? From);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username);

public sealed record TelegramUser(
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username);
