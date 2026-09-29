using System.Text.Json;
using Bizcord.Logging;
using EasyNetQ;
using RealTimeCommunicationServer.Messaging;

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
var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
// camelCase JSON og logiske event-navne, så vi kan læse ChatService's events (se contracts.md).
builder.Services
    .AddEasyNetQ($"host={rabbitMqHost}")
    .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));
// Skal registreres efter AddEasyNetQ for at overtage standard-serializeren.
builder.Services.AddSingleton<ITypeNameSerializer, EventTypeNames>();
builder.Services.AddSingleton<IMessageClient, RabbitMqMessageClient>();
builder.Services.AddMessageHandlers(typeof(Program).Assembly);
builder.Services.AddHostedService<HandleMessages>();

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

app.Run();
