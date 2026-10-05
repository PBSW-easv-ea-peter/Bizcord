namespace ChatService.Domain;

/// <summary>The action conflicts with existing state (e.g. a duplicate).</summary>
public sealed class ConflictException(string message) : DomainException(message);
