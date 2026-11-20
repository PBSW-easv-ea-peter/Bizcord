using System.Text;
using Bizcord.ApiGateway.Application.Services;
using Bizcord.ApiGateway.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Ocelot merges these source files into a generated ocelot.json at startup.
builder.Configuration.AddOcelot("Configuration", builder.Environment);

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

// Add Vault secret provider
builder.Services.AddVaultSecretProvider();

builder.Services.AddJwtTokenService();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddApiGatewayServices(builder.Configuration);

var app = builder.Build();

// Retrieve JWT signing key from Vault and configure authentication
var vaultProvider = app.Services.GetRequiredService<VaultSecretProvider>();
var jwtSigningKey = await vaultProvider.GetJwtSigningKeyAsync();

// Update JWT options with the signing key from Vault
var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
jwtOptions.AuthenticationProviderKey = jwtSigningKey;

// Configure the JWT bearer options with the signing key
var jwtBearerOptions = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
var options = jwtBearerOptions.Get(JwtBearerDefaults.AuthenticationScheme);
options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSigningKey));

app.MapGet("/custom-openapi.yaml", async context =>
{
    context.Response.ContentType = "application/yaml";

    await context.Response.SendFileAsync(
        Path.Combine(
            app.Environment.WebRootPath!,
            "custom-openapi.yaml"));
});

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/custom-openapi.yaml",
        "Bizcord API Gateway");
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

#pragma warning disable ASP0014
app.UseEndpoints(endpoints => endpoints.MapControllers());
#pragma warning restore ASP0014;

app.Use(async (context, next) =>
{
    context.Request.Headers.Remove("X-UserId");
    context.Request.Headers.Remove("X-Role");
    await next(context);
});

await app.UseOcelot();
await app.RunAsync();