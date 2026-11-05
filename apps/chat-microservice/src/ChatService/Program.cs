using Bizcord.Logging;
using ChatService.Application;
using ChatService.Infrastructure.Messaging;
using ChatService.Infrastructure.Persistence;
using ChatService.Infrastructure.Web;

var builder = WebApplication.CreateBuilder(args);

// Logging: template JSON to stdout (libs/Bizcord.Logging)
builder.AddBizcordLogging(builder.Configuration["Logging:ServiceName"] ?? "ChatService");

// Persistence: short timeouts + circuit breaker (ChatDb)
builder.Services.AddChatDb(builder.Configuration);

// Messaging
builder.Services.AddChatMessaging(builder.Configuration.RabbitMqConnectionString());
builder.Services.AddHostedService<MessageDeliveredConsumer>();

// Application
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ChatAppService>();
builder.Services.AddScoped<MessageAppService>();

// OpenAPI
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddSwaggerGen();

// Error handling (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Health
builder.Services.AddHealthChecks();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Always - otherwise DomainExceptionHandler does not map errors to 4xx in development.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Makes Program visible to WebApplicationFactory in the API tests.
public partial class Program;
