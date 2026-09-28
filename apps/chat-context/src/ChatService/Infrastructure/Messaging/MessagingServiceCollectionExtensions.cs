using System.Text.Json;
using ChatService.Application;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>EasyNetQ med logiske event-navne og camelCase JSON - se docs/contracts.md.</summary>
    public static IServiceCollection AddChatMessaging(this IServiceCollection services, string rabbitMqHost, EventTypeNames? typeNames = null)
    {
        services
            .AddEasyNetQ($"host={rabbitMqHost}")
            .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Registreres efter AddEasyNetQ, så den overtager standard-serializeren.
        services.AddSingleton<ITypeNameSerializer>(typeNames ?? EventTypeNames.ForChatService);
        services.AddSingleton<IMessageClient, RabbitMqMessageClient>();

        return services;
    }
}
