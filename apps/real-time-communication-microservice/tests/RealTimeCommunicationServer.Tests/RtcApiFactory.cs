using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.RabbitMq;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// RTC med rigtig RabbitMQ i en Testcontainer. Brugeren 'rabbitmq', fordi 'guest' kun må logge ind
/// fra localhost inde i containeren. Containeren ryddes op af Ryuk.
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
