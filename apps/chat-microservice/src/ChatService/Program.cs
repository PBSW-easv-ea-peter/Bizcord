using Bizcord.Logging;
using ChatService.Application;
using ChatService.Contracts;
using ChatService.Infrastructure.Messaging;
using ChatService.Infrastructure.Persistence;
using ChatService.Infrastructure.Web;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Logging: template JSON to stdout (libs/Bizcord.Logging)
builder.AddBizcordLogging(builder.Configuration["Logging:ServiceName"] ?? "ChatService");

// Persistence
var chatDbConnectionString = builder.Configuration.GetConnectionString("ChatDb")
    ?? throw new InvalidOperationException("Connection string 'ChatDb' is missing.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(chatDbConnectionString));
builder.Services.AddScoped<IChatRepository, DapperChatRepository>();
builder.Services.AddScoped<IMessageRepository, DapperMessageRepository>();
builder.Services.AddScoped<IReceiptRepository, DapperReceiptRepository>();
builder.Services.AddScoped<IFileMessageSagaRepository, DapperFileMessageSagaRepository>();

// Messaging
builder.Services.AddChatMessaging(builder.Configuration.RabbitMqConnectionString());
builder.Services.AddHostedService<MessageDeliveredConsumer>();
builder.Services.AddHostedService<FileSagaReplyConsumer>();
builder.Services.AddHostedService<FileSagaTimeoutService>();

// Application
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ChatAppService>();
builder.Services.AddScoped<MessageAppService>();
builder.Services.AddScoped<FileMessageSagaOrchestrator>();
// The orchestrator is the handler for every reply in the saga - same instance per scope.
builder.Services.AddScoped<IMessageHandler<QuotaReserved>>(sp => sp.GetRequiredService<FileMessageSagaOrchestrator>());
builder.Services.AddScoped<IMessageHandler<QuotaRejected>>(sp => sp.GetRequiredService<FileMessageSagaOrchestrator>());
builder.Services.AddScoped<IMessageHandler<QuotaReleased>>(sp => sp.GetRequiredService<FileMessageSagaOrchestrator>());

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
