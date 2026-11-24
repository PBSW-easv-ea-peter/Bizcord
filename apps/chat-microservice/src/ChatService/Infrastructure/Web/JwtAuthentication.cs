using System.Text;
using ChatService.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ChatService.Infrastructure.Web;

/// <summary>
/// East/West security: ChatService validates the same token as the gateway (defense in depth),
/// so a request that reaches it without passing the gateway is still rejected.
/// </summary>
public static class JwtAuthentication
{
    public const string MemberPolicy = "member";
    public const string InternalServicePolicy = "internal-service";

    public static IServiceCollection AddChatAuthentication(this IServiceCollection services, IConfiguration configuration, string signingKey)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(MemberPolicy, policy => policy.RequireClaim("role", "member"))
            .AddPolicy(InternalServicePolicy, policy => policy.RequireClaim("role", "internal-service"));

        return services;
    }

    /// <summary>
    /// The caller is the token's sub, never a header the client sent. Controllers and GraphQL keep
    /// reading X-User-Id, so this is the only place that knows the user comes from the token.
    /// </summary>
    public static IApplicationBuilder UseUserFromToken(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Request.Headers.Remove(UserHeader.Name);
            var sub = context.User.FindFirst("sub")?.Value;
            if (sub is not null)
                context.Request.Headers[UserHeader.Name] = sub;
            await next(context);
        });
}
