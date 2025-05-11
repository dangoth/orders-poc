using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Shared.RabbitMQ
{
    public interface IRabbitMQConsumer
    {
        Task InitializeAsync(string queueName, string exchangeName, string routingKey);
        Task StartConsumingAsync(string queueName, Func<string, Task> messageHandler);
        Task StopConsumingAsync();
    }

    public class RabbitMQConsumer : IRabbitMQConsumer
    {
        private readonly RabbitMQConnection _connection;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private IChannel? _channel;
        private string? _consumerTag;

        public RabbitMQConsumer(RabbitMQConnection connection, ILogger<RabbitMQConsumer> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task InitializeAsync(string queueName, string exchangeName, string routingKey)
        {
            if (!_connection.IsConnected)
            {
                await _connection.TryConnectAsync();
            }

            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: routingKey);
        }

        public async Task StartConsumingAsync(string queueName, Func<string, Task> messageHandler)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("Channel not initialized");
            }

            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, eventArgs) => 
            {
                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await messageHandler(message);
                    await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _consumerTag = await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
        }

        public async Task StopConsumingAsync()
        {
            if (_channel != null && _consumerTag != null)
            {
                await _channel.BasicCancelAsync(_consumerTag);
            }
        }
    }
}