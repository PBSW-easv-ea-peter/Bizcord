using System.Text.Json;
using Bizcord.Logging;
using EasyNetQ;
using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Realtime;

var builder = WebApplication.CreateBuilder(args);

// Logging: template-shaped JSON to stdout (libs/Bizcord.Logging)
builder.AddBizcordLogging(builder.Configuration["Logging:ServiceName"] ?? "RealTimeCommunicationService");

// OpenAPI
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddSwaggerGen();

// Error handling (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();

// Messaging
// Full connection string if set (e.g. Testcontainers), otherwise just the host as in compose (RabbitMq__Host).
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? $"host={builder.Configuration["RabbitMq:Host"] ?? "localhost"}";
// camelCase JSON and logical event names, so we can read ChatService's events (see contracts.md).
builder.Services
    .AddEasyNetQ(rabbitMqConnectionString)
    .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));
// Must be registered after AddEasyNetQ to replace the default serializer.
builder.Services.AddSingleton<ITypeNameSerializer, EventTypeNames>();
builder.Services.AddSingleton<IMessageClient, RabbitMqMessageClient>();
builder.Services.AddMessageHandlers(typeof(Program).Assembly);
builder.Services.AddHostedService<HandleMessages>();

// Real-time push (SignalR)
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, QueryStringUserIdProvider>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton<IClientNotifier, SignalRClientNotifier>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}
else
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<ChatHub>(ChatHub.Path);

app.Run();

// Makes Program visible to WebApplicationFactory in the tests.
public partial class Program;
