namespace ChatService.Controllers;

/// <summary>
/// Hvem udfører handlingen. Brugere ejes af UserService - vi stoler på headeren i MVP.
/// Erstattes af et UserAuth-token senere; så er det kun her, der skal ændres.
/// </summary>
internal static class UserHeader
{
    public const string Name = "X-User-Id";
}
