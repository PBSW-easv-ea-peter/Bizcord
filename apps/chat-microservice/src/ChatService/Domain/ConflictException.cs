namespace ChatService.Domain;

/// <summary>Handlingen er i konflikt med eksisterende tilstand (fx dublet).</summary>
public sealed class ConflictException(string message) : DomainException(message);
