namespace ChatService.Domain;

/// <summary>Handlingen er gyldig, men brugeren må ikke udføre den.</summary>
public sealed class NotAllowedException(string message) : DomainException(message);
