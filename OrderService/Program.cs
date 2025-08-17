using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OrderService.Configuration;
using OrderService.Persistence;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add all services using the extension method
builder.Services.AddOrderServiceDependencies(builder.Configuration);
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
    await dbContext.Database.MigrateAsync();
    
    var rabbitMQInit = scope.ServiceProvider.GetRequiredService<IRabbitMQInitializationService>();
    await rabbitMQInit.InitializeAsync();
    
    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
    await orderService.InitializeAsync();
    
    var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
    await inventoryService.SeedInventoryDataAsync();
}

app.Run();
