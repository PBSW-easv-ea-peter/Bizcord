using System.Reflection;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>The message types that have at least one handler. HandleMessages subscribes to exactly these.</summary>
public class MessageHandlerRegistry
{
    public MessageHandlerRegistry(
        IReadOnlyCollection<Type> messageTypes)
    {
        MessageTypes = messageTypes;
    }

    public IReadOnlyCollection<Type> MessageTypes { get; }
}

public static class MessageHandlerRegistration
{
    /// <summary>
    /// Scans the assembly for concrete classes that implement IMessageHandler&lt;T&gt;,
    /// and registers them as scoped (one scope per message, like one per HTTP request).
    /// </summary>
    public static IServiceCollection AddMessageHandlers(
        this IServiceCollection services,
        Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                .Select(handlerInterface => (Interface: handlerInterface, Implementation: type)))
            .ToList();

        foreach (var (handlerInterface, implementation) in handlers)
            services.AddScoped(handlerInterface, implementation);

        var messageTypes = handlers
            .Select(handler => handler.Interface.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        services.AddSingleton(new MessageHandlerRegistry(messageTypes));

        return services;
    }
}
