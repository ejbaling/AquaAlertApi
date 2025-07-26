using Serilog;
using AquaAlertApi;
using AquaAlertApi.Services.MqttClientService;
using MassTransit;
using AquaAlertApi.Services;
using AquaAlertApi.Contracts;
using AquaAlertApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Register the MQTT client service
builder.Services.AddHostedService<MqttClientService>();

// Register MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Add all consumers in your assembly automatically
    x.AddConsumer<RabbitMQConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        // Auto-configure endpoints for all consumers
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Test endpoint to publish manually
app.MapGet("/test", async (IPublishEndpoint publishEndpoint) =>
{
    await publishEndpoint.Publish(new MqttMessage
    {
        ClientId = "TestClient",
        Distance = (decimal?)12.34,
        Unit = "cm"
    });

    return "Published test message!";
});

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

await app.RunAsync();

namespace AquaAlertApi
{
    record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
