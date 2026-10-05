using System.Text.Json;
using ChatService.Application;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Fuld connection string, hvis den er sat (fx fra Testcontainers: tilfældig port og egen bruger),
    /// ellers kun host - som compose-filerne sætter via RabbitMq__Host.
    /// </summary>
    public static string RabbitMqConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq") ?? $"host={configuration["RabbitMq:Host"] ?? "localhost"}";

    /// <summary>EasyNetQ med logiske event-navne og camelCase JSON - se docs/contracts.md.</summary>
    public static IServiceCollection AddChatMessaging(this IServiceCollection services, string rabbitMqConnectionString, EventTypeNames? typeNames = null)
    {
        services
            .AddEasyNetQ(rabbitMqConnectionString)
            .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Registreres efter AddEasyNetQ, så den overtager standard-serializeren.
        services.AddSingleton<ITypeNameSerializer>(typeNames ?? EventTypeNames.ForChatService);
        services.AddSingleton<IMessageClient, RabbitMqMessageClient>();

        return services;
    }
}
