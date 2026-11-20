using ChatService.Application;
using Npgsql;

namespace ChatService.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>chatDB with short timeouts and a circuit breaker (section "ChatDb"), plus the repositories.</summary>
    public static IServiceCollection AddChatDb(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChatDb")
            ?? throw new InvalidOperationException("Connection string 'ChatDb' is missing.");
        var options = configuration.GetSection(ChatDbOptions.SectionName).Get<ChatDbOptions>() ?? new ChatDbOptions();

        // Overrides the connection string, so an environment that forgets the setting can't bring back Npgsql's 30 s.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.ConnectionStringBuilder.Timeout = options.TimeoutSeconds;
        dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = options.CommandTimeoutSeconds;

        services.AddSingleton(dataSourceBuilder.Build());
        services.AddSingleton(options);
        services.AddSingleton<ChatDb>();
        services.AddScoped<IChatRepository, DapperChatRepository>();
        services.AddScoped<IMessageRepository, DapperMessageRepository>();
        services.AddScoped<IReceiptRepository, DapperReceiptRepository>();

        return services;
    }
}
