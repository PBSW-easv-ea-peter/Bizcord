using Bizcord.Logging;
using FileService.Application;
using FileService.Contracts;
using FileService.Domain;
using FileService.Infrastructure.Messaging;
using FileService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddBizcordLogging(builder.Configuration["Logging:ServiceName"] ?? "FileService");

// Persistence
var fileDbConnectionString = builder.Configuration.GetConnectionString("FileDb")
    ?? throw new InvalidOperationException("Connection string 'FileDb' is missing.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(fileDbConnectionString));
builder.Services.AddScoped<IFileRepository, DapperFileRepository>();

// Messaging
builder.Services.AddFileMessaging(builder.Configuration.RabbitMqConnectionString());
builder.Services.AddHostedService<QuotaCommandConsumer>();

// Application
builder.Services.AddSingleton(builder.Configuration.GetSection("Quota").Get<QuotaSettings>() ?? new QuotaSettings());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<FileAppService>();
builder.Services.AddScoped<QuotaSagaParticipant>();
builder.Services.AddScoped<IMessageHandler<ReserveQuota>>(sp => sp.GetRequiredService<QuotaSagaParticipant>());
builder.Services.AddScoped<IMessageHandler<ReleaseQuota>>(sp => sp.GetRequiredService<QuotaSagaParticipant>());

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

var app = builder.Build();

// Invalid uploads (empty, too large, no name) → 400 ProblemDetails. Everything else → 500.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is FileDomainException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    context.Response.StatusCode = status;

    await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context,
        ProblemDetails = { Status = status, Detail = status == StatusCodes.Status400BadRequest ? exception!.Message : null }
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
