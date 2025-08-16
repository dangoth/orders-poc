using RestockingService.Services;
using Shared.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRabbitMQ("rabbitmq");
builder.Services.AddScoped<IRestockingService, RestockingService.Services.RestockingService>();
builder.Services.AddHealthChecks()
    .AddRabbitMQ("amqp://guest:guest@rabbitmq:5672/", name: "rabbitmq");

var app = builder.Build();

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
    var restockingService = scope.ServiceProvider.GetRequiredService<IRestockingService>();
    await restockingService.InitializeAsync();
}

app.Run();