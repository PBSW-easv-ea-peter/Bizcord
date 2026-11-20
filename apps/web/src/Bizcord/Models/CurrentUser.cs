namespace Bizcord.Models;

/// <summary>
/// "Dev login": the user you are logged in as is picked in the top bar dropdown and kept in a cookie.
/// Stands in for a real login while there is no UserAuth service. With one, the user id would come from the
/// login token's claims, and the rest of the frontend would stay the same.
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor)
{
    public const string CookieName = "bizcord-user";

    /// <summary>The user from the cookie, or the first test user if the cookie is missing or invalid.</summary>
    public TestUser User
    {
        get
        {
            var cookie = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
            return Guid.TryParse(cookie, out var id)
                ? TestUsers.CanLogIn.FirstOrDefault(u => u.Id == id) ?? TestUsers.CanLogIn[0]
                : TestUsers.CanLogIn[0];
        }
    }

    public Guid Id => User.Id;
    public string Name => User.Name;
}
