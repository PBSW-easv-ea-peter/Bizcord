using ChatService.Application;
using ChatService.Infrastructure.Messaging;
using ChatService.Tests.Application;
using ChatService.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Tests.Api;

/// <summary>Rigtig DB (Testcontainers), men fake broker - API-testene kræver ikke RabbitMQ.</summary>
public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    internal InMemoryMessageClient MessageClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ChatDb", TestDatabase.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMessageClient>(MessageClient);

            // Consumeren ville forbinde til RabbitMQ ved opstart - den testes i MessageDeliveredConsumerTests.
            services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(MessageDeliveredConsumer)));
        });
    }
}
