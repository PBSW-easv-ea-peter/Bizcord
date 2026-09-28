namespace ChatService.Domain;

/// <summary>Kastes når en domæne-invariant brydes.</summary>
public class DomainException(string message) : Exception(message);
