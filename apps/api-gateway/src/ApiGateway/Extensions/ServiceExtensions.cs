using System.Text;
using Bizcord.ApiGateway.Application.Services;
using Bizcord.ApiGateway.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;

namespace Bizcord.ApiGateway.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddJwtTokenService(this IServiceCollection services)
    {
        services.AddSingleton<JwtTokenService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
            return new JwtTokenService(options);
        });
        return services;
    }

    public static IServiceCollection AddApiGatewayServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                         ?? throw new InvalidOperationException("Jwt configuration is required.");

        // Signing key will be retrieved from Vault at startup

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateLifetime = true,

                    ValidateIssuerSigningKey = true,
                    // Signing key will be set at startup from Vault
                    IssuerSigningKey = null!,

                    NameClaimType = "sub",
                    RoleClaimType = "role",

                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        services.AddOcelot(configuration).AddDownstreamQoS(configuration);

        return services;
    }

    public static IServiceCollection AddVaultSecretProvider(this IServiceCollection services)
    {
        services.AddSingleton<VaultSecretProvider>();
        return services;
    }
}
