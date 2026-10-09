using System.Text.Json;
using ChatService.Application;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Full connection string if set (e.g. from Testcontainers: random port and its own user),
    /// otherwise just the host - which the compose files set via RabbitMq__Host.
    /// </summary>
    public static string RabbitMqConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq") ?? $"host={configuration["RabbitMq:Host"] ?? "localhost"}";

    /// <summary>EasyNetQ with logical event names and camelCase JSON - see docs/contracts.md.</summary>
    public static IServiceCollection AddChatMessaging(this IServiceCollection services, string rabbitMqConnectionString, EventTypeNames? typeNames = null)
    {
        services
            .AddEasyNetQ(rabbitMqConnectionString)
            .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Registered after AddEasyNetQ, so it replaces the default serializer.
        services.AddSingleton<ITypeNameSerializer>(typeNames ?? EventTypeNames.ForChatService);
        services.AddSingleton<IMessageClient, RabbitMqMessageClient>();

        return services;
    }
}
