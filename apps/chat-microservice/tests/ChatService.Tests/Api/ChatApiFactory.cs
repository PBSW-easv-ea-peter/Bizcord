using ChatService.Application;
using ChatService.Infrastructure.Messaging;
using ChatService.Infrastructure.Web;
using ChatService.Tests.Application;
using ChatService.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChatService.Tests.Api;

/// <summary>Real DB (Testcontainers), but a fake broker - the API tests don't need RabbitMQ.</summary>
public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public ChatApiFactory() : this(TestDatabase.ConnectionString, new Dictionary<string, string?>())
    {
    }

    /// <summary>Own database and extra settings - e.g. for tests that pause Postgres.</summary>
    internal ChatApiFactory(string connectionString, IReadOnlyDictionary<string, string?> settings)
    {
        _connectionString = connectionString;
        _settings = settings;
    }

    internal InMemoryMessageClient MessageClient { get; } = new();

    /// <summary>What DomainExceptionHandler logs - replaces its console output in the API tests.</summary>
    internal CapturingLogger<DomainExceptionHandler> HandlerLog { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ChatDb", _connectionString);
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMessageClient>(MessageClient);
            services.AddSingleton<ILogger<DomainExceptionHandler>>(HandlerLog);

            // The consumer would connect to RabbitMQ on startup - it is tested in MessageDeliveredConsumerTests.
            services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(MessageDeliveredConsumer)));
        });
    }
}
