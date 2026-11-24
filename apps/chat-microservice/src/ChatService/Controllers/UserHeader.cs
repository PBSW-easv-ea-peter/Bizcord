namespace ChatService.Controllers;

/// <summary>
/// Who performs the action. The header is set from the validated token's sub by JwtAuthentication.UseUserFromToken,
/// so a header sent by the client is never trusted.
/// </summary>
internal static class UserHeader
{
    public const string Name = "X-User-Id";
}
