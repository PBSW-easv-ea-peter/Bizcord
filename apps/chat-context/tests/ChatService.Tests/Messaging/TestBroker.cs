using Testcontainers.RabbitMq;

namespace ChatService.Tests.Messaging;

/// <summary>
/// Én RabbitMQ-container pr. testkørsel (Testcontainers). Tilfældig port og brugeren 'rabbitmq' -
/// 'guest' må kun logge ind fra localhost inde i containeren.
/// </summary>
internal static class TestBroker
{
    private const string User = "rabbitmq";

    private static readonly RabbitMqContainer Container = Start();

    public static string Host => Container.Hostname;

    public static int Port => Container.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

    public static string UserName => User;

    public static string Password => User;

    /// <summary>EasyNetQ-format, samme som ConnectionStrings:RabbitMq i appsettings.</summary>
    public static string ConnectionString => $"host={Host}:{Port};username={User};password={User}";

    private static RabbitMqContainer Start()
    {
        var container = new RabbitMqBuilder("rabbitmq:3-alpine")
            .WithUsername(User)
            .WithPassword(User)
            .Build();

        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }
}
