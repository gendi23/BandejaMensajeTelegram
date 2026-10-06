using System.Text;
using System.Text.Json;
using Bandeja.Application;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Bandeja.Infrastructure;

public sealed class RabbitMessageBus(IConfiguration configuration) : IMessagePublisher
{
    public Task PublishAsync(IncomingTelegramMessage message, CancellationToken cancellationToken)
    {
        var factory = RabbitConnectionFactory(configuration);
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitTopology.Declare(channel);
        channel.ConfirmSelect();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        channel.BasicPublish(RabbitTopology.Exchange, RabbitTopology.RoutingKey, properties, body);
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }

    internal static ConnectionFactory RabbitConnectionFactory(IConfiguration configuration) => new()
    {
        Uri = new Uri(configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@rabbitmq:5672/"),
        DispatchConsumersAsync = false,
        AutomaticRecoveryEnabled = true
    };
}

internal static class RabbitTopology
{
    public const string Exchange = "telegram.incoming.exchange";
    public const string Queue = "telegram.incoming";
    public const string RoutingKey = "telegram.incoming";
    public const string DeadExchange = "telegram.dead.exchange";
    public const string DeadQueue = "telegram.dead";
    public const string DeadRoutingKey = "telegram.dead";

    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(Exchange, ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare(DeadExchange, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(DeadQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(DeadQueue, DeadExchange, DeadRoutingKey);
        var arguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DeadExchange,
            ["x-dead-letter-routing-key"] = DeadRoutingKey
        };
        channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, arguments);
        channel.QueueBind(Queue, Exchange, RoutingKey);
    }
}
