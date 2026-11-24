using Microsoft.Extensions.DependencyInjection.Extensions;
using Ocelot.DependencyInjection;

namespace Bizcord.ApiGateway.Resilience;

public static class DownstreamQoSExtensions
{
    public static IOcelotBuilder AddDownstreamQoS(this IOcelotBuilder builder, IConfiguration configuration)
    {
        builder.Services.Configure<DownstreamQoSOptions>(configuration.GetSection(DownstreamQoSOptions.Section));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DownstreamQoS>();

        // Not global: routes opt in, so long-lived SignalR requests are not cut off by the timeout.
        builder.AddDelegatingHandler<DownstreamQoSHandler>(global: false);
        return builder;
    }
}
