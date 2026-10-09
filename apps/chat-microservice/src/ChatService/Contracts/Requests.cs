using System.ComponentModel.DataAnnotations;

namespace ChatService.Contracts;

// Only the shape is validated here - content rules (length, emptiness) live in the domain.

public sealed record CreateDirectChatRequest([Required] Guid? OtherUserId);

public sealed record CreateGroupChatRequest(string Title);

public sealed record AddParticipantRequest([Required] Guid? UserId);

public sealed record SendMessageRequest(string Content);

public sealed record EditMessageRequest(string Content);
