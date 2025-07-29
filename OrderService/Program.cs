using OrderService.Services;
using Shared.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using OrderService.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRabbitMQ("rabbitmq");
builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();
builder.Services.AddHealthChecks()
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

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
    }
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    
    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
    await orderService.InitializeAsync();
}

app.Run();
