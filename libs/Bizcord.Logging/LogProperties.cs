namespace Bizcord.Logging;

/// <summary>Property names that the formatter pulls out of Payload and places in the template's fixed fields.</summary>
internal static class LogProperties
{
    public const string Service = "Service";
    public const string FilePath = "FilePath";
    public const string LineNumber = "LineNumber";
    public const string MemberName = "MemberName";
    public const string ParentId = "ParentId";

    public static readonly HashSet<string> Reserved = [Service, FilePath, LineNumber, MemberName, ParentId];
}
