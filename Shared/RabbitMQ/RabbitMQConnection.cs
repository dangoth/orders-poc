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

        public RabbitMQConnection(string hostName, ILogger<RabbitMQConnection> logger)
        {
            _connectionFactory = new ConnectionFactory { HostName = hostName };
            _logger = logger;
        }

        public bool IsConnected => _connection != null && _connection.IsOpen && !_disposed;

        public async Task<bool> TryConnectAsync()
        {
            try
            {
                if (IsConnected)
                    return true;

                _connection = await _connectionFactory.CreateConnectionAsync();

                _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;

                _logger.LogInformation("RabbitMQ connection established");
                return true;
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogError(ex, "RabbitMQ connection failed");
                return false;
            }
        }


        private Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown. Reason: {0}", e.ReplyText);
            return Task.CompletedTask;
        }

        private Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
        {
            _logger.LogWarning("RabbitMQ callback exception. Exception: {0}", e.Exception.Message);
            return Task.CompletedTask;
        }

        private Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection blocked. Reason: {0}", e.Reason);
            return Task.CompletedTask;
        }

        public async Task<IChannel> CreateChannelAsync()
        {
            if (!IsConnected)
            {
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
                    {
                        _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                        _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                        _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                        await _connection.CloseAsync();
                    }
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