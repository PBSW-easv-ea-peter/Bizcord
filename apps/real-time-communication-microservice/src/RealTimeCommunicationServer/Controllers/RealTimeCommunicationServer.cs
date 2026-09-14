using Microsoft.AspNetCore.Mvc;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Models;

namespace RealTimeCommunicationServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageClient _messageClient;

    public MessagesController(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] PingMessage pingMessage,
        CancellationToken cancellationToken)
    {
        await _messageClient.Publish(pingMessage, cancellationToken);

        return Accepted();
    }
}