using Microsoft.EntityFrameworkCore;
using OrderService.Persistence;
using OrderService.Repositories;
using OrderService.Services;
using RabbitMQ.Client;
using Shared.RabbitMQ;

namespace OrderService.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOrderServiceDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddRabbitMQ("rabbitmq");

            services.AddScoped<IOrderService, Services.OrderService>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IEventStoreService, EventStoreService>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<ILowStockWarningService, LowStockWarningService>();
            services.AddScoped<IRabbitMQInitializationService, RabbitMQInitializationService>();
            services.AddScoped<Shared.Services.IEventPublishingHelper, Shared.Services.EventPublishingHelper>();

            services.AddHealthChecks()
                .AddRabbitMQ(sp => 
                {
                    var factory = new ConnectionFactory 
                    { 
                        HostName = "rabbitmq",
                        UserName = "guest",
                        Password = "guest"
                    };  
                    return Task.FromResult(factory.CreateConnectionAsync().GetAwaiter().GetResult());
                }, name: "rabbitmq");

            services.AddDbContext<OrderDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            return services;
        }
    }
}