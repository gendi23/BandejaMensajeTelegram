using System.Text;
using System.Text.Json;
using Bandeja.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bandeja.Infrastructure;

public sealed class RabbitMessageConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMessageConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = RabbitMessageBus.RabbitConnectionFactory(configuration).CreateConnection();
                using var channel = connection.CreateModel();
                RabbitTopology.Declare(channel);
                channel.BasicQos(0, 1, global: false);
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (_, delivery) => ProcessDelivery(channel, delivery, stoppingToken);
                channel.BasicConsume(RabbitTopology.Queue, autoAck: false, consumer);
                logger.LogInformation("Consuming Telegram updates from {Queue}", RabbitTopology.Queue);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer disconnected; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void ProcessDelivery(IModel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<IncomingTelegramMessage>(Encoding.UTF8.GetString(delivery.Body.Span))
                ?? throw new JsonException("Empty Telegram update");
            using var scope = scopeFactory.CreateScope();
            var inbox = scope.ServiceProvider.GetRequiredService<InboxService>();
            inbox.ReceiveAsync(message, cancellationToken).GetAwaiter().GetResult();
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process Telegram delivery {DeliveryTag}; moving to dead-letter queue", delivery.DeliveryTag);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
