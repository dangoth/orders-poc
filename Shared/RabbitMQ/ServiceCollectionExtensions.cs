using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Shared.RabbitMQ
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRabbitMQ(this IServiceCollection services, string hostName)
        {
            services.AddSingleton<ConnectionFactory>(sp =>
            {
                return new ConnectionFactory
                {
                    HostName = "host.docker.internal",
                    UserName = "guest",
                    Password = "guest",
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat = TimeSpan.FromSeconds(30)
                };
            });

            services.AddSingleton<RabbitMQConnection>();
            services.AddSingleton<IRabbitMQConnection>(sp => sp.GetRequiredService<RabbitMQConnection>());
            services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();
            services.AddSingleton<IRabbitMQConsumer, RabbitMQConsumer>();

            return services;
        }
    }
}