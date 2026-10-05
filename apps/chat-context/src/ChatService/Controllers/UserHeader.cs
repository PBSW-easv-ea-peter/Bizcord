namespace ChatService.Controllers;

/// <summary>
/// Who performs the action. Users are owned by UserService - we trust the header in the MVP.
/// Will be replaced by a UserAuth token later; then this is the only place that needs to change.
/// </summary>
internal static class UserHeader
{
    public const string Name = "X-User-Id";
}
