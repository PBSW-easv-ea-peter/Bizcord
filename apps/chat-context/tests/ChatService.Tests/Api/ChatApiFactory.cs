using ChatService.Application;
using ChatService.Tests.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Tests.Api;

/// <summary>Rigtig DB, men fake broker - API-testene kræver ikke RabbitMQ.</summary>
public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    internal InMemoryMessageClient MessageClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddSingleton<IMessageClient>(MessageClient));
}
