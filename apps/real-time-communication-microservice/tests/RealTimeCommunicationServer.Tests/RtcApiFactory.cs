using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.RabbitMq;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// RTC with a real RabbitMQ in a Testcontainer. User 'rabbitmq', because 'guest' may only log in
/// from localhost inside the container. The container is cleaned up by Ryuk.
/// </summary>
public sealed class RtcApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string User = "rabbitmq";

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-alpine")
        .WithUsername(User)
        .WithPassword(User)
        .Build();

    public Task InitializeAsync() => _rabbitMq.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting(
            "ConnectionStrings:RabbitMq",
            $"host={_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort)};username={User};password={User}");

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
