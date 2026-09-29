using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Bizcord.Logging;

public static class LoggingSetup
{
    /// <summary>Erstatter standard-loggeren med Serilog, som skriver skabelon-JSON til stdout.</summary>
    public static IHostApplicationBuilder AddBizcordLogging(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog(config => config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty(LogProperties.Service, serviceName)
            .Enrich.With<ActivityParentEnricher>()
            .WriteTo.Console(new TemplateJsonFormatter()));

        return builder;
    }
}
