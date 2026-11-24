using Bizcord.Models;
using Bizcord.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// The frontend only talks to the API gateway, never to a service directly.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GatewayTokenCache>();
builder.Services.AddSingleton<KnownChats>();
builder.Services.AddHttpClient<GatewayClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayApi:BaseUrl"] ?? "http://localhost:8080/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// "Dev login": the chosen test user is read from a cookie (there is no UserService yet)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();

var app = builder.Build();

// Switch test user from the top bar dropdown. Stores the user id in the cookie and goes back to the page.
app.MapGet("/switch-user/{id:guid}", (Guid id, string? returnUrl, HttpContext context) =>
{
    if (TestUsers.CanLogIn.Any(u => u.Id == id))
    {
        context.Response.Cookies.Append(CurrentUser.CookieName, id.ToString(),
            new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
    }

    // Local addresses only, so the link can't send you on to another site (open redirect)
    var isLocal = returnUrl is not null && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\");
    return Results.LocalRedirect(isLocal ? returnUrl! : "/");
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
