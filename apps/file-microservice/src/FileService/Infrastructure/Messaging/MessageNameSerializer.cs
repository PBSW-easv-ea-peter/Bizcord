using System.Text.Json;
using EasyNetQ;
using FileService.Application;
using FileService.Contracts;

namespace FileService.Infrastructure.Messaging;

/// <summary>Logical message names instead of C# type names - so ChatService can use its own types (see docs/contracts.md).</summary>
public sealed class MessageNameSerializer : ITypeNameSerializer
{
    private static readonly IReadOnlyDictionary<Type, string> Names = new Dictionary<Type, string>
    {
        [typeof(ReserveQuota)] = MessageNames.ReserveQuota,
        [typeof(ReleaseQuota)] = MessageNames.ReleaseQuota,
        [typeof(QuotaReserved)] = MessageNames.QuotaReserved,
        [typeof(QuotaRejected)] = MessageNames.QuotaRejected,
        [typeof(QuotaReleased)] = MessageNames.QuotaReleased,
        [typeof(MessageDeleted)] = ChatEventNames.MessageDeleted
    };

    private static readonly IReadOnlyDictionary<string, Type> Types = Names.ToDictionary(pair => pair.Value, pair => pair.Key);

    private readonly DefaultTypeNameSerializer _fallback = new();

    public string Serialize(Type type) => Names.TryGetValue(type, out var name) ? name : _fallback.Serialize(type);

    public Type Deserialize(string typeName) => Types.TryGetValue(typeName, out var type) ? type : _fallback.Deserialize(typeName);
}

public static class MessagingServiceCollectionExtensions
{
    public static string RabbitMqConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("RabbitMq") ?? $"host={configuration["RabbitMq:Host"] ?? "localhost"}";

    public static IServiceCollection AddFileMessaging(this IServiceCollection services, string rabbitMqConnectionString)
    {
        services
            .AddEasyNetQ(rabbitMqConnectionString)
            .UseSystemTextJson(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // After AddEasyNetQ, so it replaces the default.
        services.AddSingleton<ITypeNameSerializer, MessageNameSerializer>();
        services.AddSingleton<IMessageClient, RabbitMqMessageClient>();

        return services;
    }
}
