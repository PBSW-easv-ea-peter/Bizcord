using System.Text;
using Bizcord.ApiGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;

namespace Bizcord.ApiGateway.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddJwtTokenService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new JwtTokenService(GetSigningKey(configuration)));
        return services;
    }

    public static IServiceCollection AddApiGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = GetSigningKey(configuration);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Preserve sub, role and scope for Ocelot's claim lookups.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();
        services.AddOcelot(configuration);
        return services;
    }

    private static string GetSigningKey(IConfiguration configuration) =>
        configuration["AuthenticationProviderKey"]
        ?? throw new InvalidOperationException("AuthenticationProviderKey is required.");
}
