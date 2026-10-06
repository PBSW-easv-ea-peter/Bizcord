namespace ChatService.Domain;

public sealed record MessageContent
{
    public const int MaxLength = 4000;

    public string Value { get; }

    public MessageContent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Message content cannot be empty.");

        if (value.Length > MaxLength)
            throw new DomainException($"Message content cannot exceed {MaxLength} characters.");

        Value = value;
    }

    public override string ToString() => Value;
}
