using ProcessingService.Services;
using Shared.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRabbitMQ("localhost");
builder.Services.AddScoped<IProcessingService, ProcessingService.Services.ProcessingService>();
builder.Services.AddHealthChecks()
    .AddRabbitMQ("amqp://guest:guest@rabbitmq:5672/", name: "rabbitmq");

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
    var processingService = scope.ServiceProvider.GetRequiredService<IProcessingService>();
    await processingService.InitializeAsync();
}

app.Run();
