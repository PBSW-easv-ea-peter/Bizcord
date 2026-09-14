using EasyNetQ;
using RealTimeCommunicationServer.Messaging;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddSwaggerGen();

// Error handling (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();

// Messaging
var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
builder.Services.AddEasyNetQ($"host={rabbitMqHost}");
builder.Services.AddSingleton<IMessageClient, RabbitMqMessageClient>();
builder.Services.AddHostedService<HandleMessages>();

builder.Services.AddControllers();
builder.Services.AddLogging();

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
