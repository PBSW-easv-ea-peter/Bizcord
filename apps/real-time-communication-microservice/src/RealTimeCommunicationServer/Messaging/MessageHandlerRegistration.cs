using System.Reflection;

namespace RealTimeCommunicationServer.Messaging;

/// <summary>De beskedtyper, der har mindst én handler. HandleMessages abonnerer på præcis disse.</summary>
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
    /// Scanner assembly'en for konkrete klasser, der implementerer IMessageHandler&lt;T&gt;,
    /// og registrerer dem som scoped (ét scope pr. besked, ligesom ét pr. HTTP-request).
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
