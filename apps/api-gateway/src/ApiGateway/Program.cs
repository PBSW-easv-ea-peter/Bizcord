using Bizcord.ApiGateway.Extensions;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Ocelot merges these source files into a generated ocelot.json at startup.
builder.Configuration.AddOcelot("Configuration", builder.Environment);
builder.Services.AddJwtTokenService(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApiGatewayServices(builder.Configuration);

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Execute the faucet controller before Ocelot's terminal proxy pipeline.
#pragma warning disable ASP0014
app.UseEndpoints(endpoints => endpoints.MapControllers());
#pragma warning restore ASP0014
// These exercise headers must come from validated token claims, not the client.
app.Use(async (context, next) =>
{
    context.Request.Headers.Remove("X-UserId");
    context.Request.Headers.Remove("X-Role");
    await next(context);
});
await app.UseOcelot();
await app.RunAsync();
