namespace ChatService.Domain;

/// <summary>The action is valid, but the user is not allowed to perform it.</summary>
public sealed class NotAllowedException(string message) : DomainException(message);
