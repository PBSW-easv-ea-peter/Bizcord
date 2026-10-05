using System.Text.Json;
using Bizcord.Logging;
using EasyNetQ;
using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Realtime;

var builder = WebApplication.CreateBuilder(args);

// Logging: skabelon-JSON til stdout (libs/Bizcord.Logging)
builder.AddBizcordLogging(builder.Configuration["Logging:ServiceName"] ?? "RealTimeCommunicationService");

// OpenAPI
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddSwaggerGen();

// Error handling (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();

// Messaging
// Fuld connection string, hvis den er sat (fx Testcontainers), ellers kun host som i compose (RabbitMq__Host).
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? $"host={builder.Configuration["RabbitMq:Host"] ?? "localhost"}";
// camelCase JSON og logiske event-navne, så vi kan læse ChatService's events (se contracts.md).
builder.Services
    .AddEasyNetQ(rabbitMqConnectionString)
    .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));
// Skal registreres efter AddEasyNetQ for at overtage standard-serializeren.
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

// Gør Program synlig for WebApplicationFactory i testene.
public partial class Program;
