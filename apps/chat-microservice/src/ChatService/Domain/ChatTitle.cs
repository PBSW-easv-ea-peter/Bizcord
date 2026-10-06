namespace ChatService.Domain;

public sealed record ChatTitle
{
    public const int MaxLength = 200;

    public string Value { get; }

    public ChatTitle(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length is 0 or > MaxLength)
            throw new DomainException($"Chat title must be between 1 and {MaxLength} characters.");

        Value = trimmed;
    }

    public override string ToString() => Value;
}
