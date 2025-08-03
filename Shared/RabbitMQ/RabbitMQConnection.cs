using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Shared.RabbitMQ
{
    public interface IRabbitMQConnection : IAsyncDisposable
    {
        bool IsConnected { get; }
        Task<bool> TryConnectAsync();
        Task<IChannel> CreateChannelAsync();
    }

    public class RabbitMQConnection : IRabbitMQConnection
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly ILogger<RabbitMQConnection> _logger;
        private IConnection? _connection;
        private bool _disposed;
        private const int MaxRetries = 5;
        private const int RetryDelayMs = 2000;

        public RabbitMQConnection(ConnectionFactory factory, ILogger<RabbitMQConnection> logger)
        {
            _connectionFactory = factory;
            _logger = logger;
        }

        public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;

        public async Task<bool> TryConnectAsync()
        {
            if (IsConnected)
                return true;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    _connection = await _connectionFactory.CreateConnectionAsync();
                    _connection.ConnectionShutdownAsync += (_, e) => 
                    {
                        _logger.LogWarning("RabbitMQ connection shutdown. Reason: {0}", e.ReplyText);
                        return Task.CompletedTask;
                    };

                    _logger.LogInformation("RabbitMQ connection established");
                    return true;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning(ex, "RabbitMQ connection attempt {Attempt} failed. Retrying in {Delay}ms...", i + 1, RetryDelayMs);
                    if (i < MaxRetries - 1)
                        await Task.Delay(RetryDelayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while connecting to RabbitMQ");
                    return false;
                }
            }

            _logger.LogError("Failed to connect to RabbitMQ after {MaxRetries} attempts", MaxRetries);
            return false;
        }

        public async Task<IChannel> CreateChannelAsync()
        {
            if (!IsConnected)
            {
                var connected = await TryConnectAsync();
                if (!connected)
                    throw new InvalidOperationException("RabbitMQ is not connected");
            }

            return await _connection!.CreateChannelAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            if (_connection != null)
            {
                try
                {
                    if (_connection.IsOpen)
                        await _connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing RabbitMQ connection");
                }
                finally
                {
                    _connection.Dispose();
                }
            }

            _disposed = true;
        }
    }
}