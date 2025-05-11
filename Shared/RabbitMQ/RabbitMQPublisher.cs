using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace Shared.RabbitMQ
{
    public interface IRabbitMQPublisher
    {
        Task InitializeAsync(string exchangeName, string exchangeType);
        Task PublishAsync(string exchangeName, string routingKey, string message);
    }
    public class RabbitMQPublisher : IRabbitMQPublisher
    {
        private readonly RabbitMQConnection _connection;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private IChannel? _channel;

        public RabbitMQPublisher(RabbitMQConnection connection, ILogger<RabbitMQPublisher> logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task InitializeAsync(string exchangeName, string exchangeType)
        {
            if (!_connection.IsConnected)
            {
                await _connection.TryConnectAsync();
            }

            _channel = await _connection.CreateChannelAsync();
            await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: exchangeType, durable: true);
        }

        public async Task PublishAsync(string exchangeName, string routingKey, string message)
        {
            if (_channel == null)
            {
                throw new InvalidOperationException("Channel not initialized");
            }

            var body = Encoding.UTF8.GetBytes(message);
            var properties = new BasicProperties();

            await _channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: default);

            _logger.LogInformation("Message published: {0}", message);
        }
    }
}