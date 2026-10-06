using System.ComponentModel.DataAnnotations;

namespace ChatService.Contracts;

// Kun formen valideres her - indholdsregler (længde, tomhed) ligger i domænet.

public sealed record CreateDirectChatRequest([Required] Guid? OtherUserId);

public sealed record CreateGroupChatRequest(string Title);

public sealed record AddParticipantRequest([Required] Guid? UserId);

public sealed record SendMessageRequest(string Content);

public sealed record EditMessageRequest(string Content);
