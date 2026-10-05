# First Feature & Testing

## Contents

- [The Scenario](#the-scenario)
- [The Shared Contract](#the-shared-contract)
- [Your Assignment: Two Tracks](#your-assignment-two-tracks)
  - [Track A: If you are building the Messaging Service](#track-a-if-you-are-building-the-messaging-service)
    - [The Feature](#the-feature)
    - [The Tests](#the-tests)
  - [Track B: Everyone Else](#track-b-everyone-else)
    - [The Feature](#the-feature-1)
    - [The Tests](#the-tests-1)
- [A Note on Contracts](#a-note-on-contracts)

## The Scenario

You are building a Discord clone. Each team owns one microservice. Together they form a system. The central event that connects most of them is a user posting a message in a channel.

```
User
 │
 ▼
Messaging Service  ──publishes──▶  MessagePostedEvent
                                         │
                          ┌──────────────┼──────────────┐
                          ▼              ▼              ▼
                   Notification     Presence        Search
                    Service         Service         Service
                        │
                       ...
```

Every team's work this week connects to this flow, either by publishing a `MessagePostedEvent` or by reacting to it.

## The Shared Contract

Add this to `packages/Shared.Contracts` if it isn't there already. This is the agreement the entire system is built on this week.

```csharp
// packages/Shared.Contracts/Events/MessagePostedEvent.cs
public class MessagePostedEvent
{
    public Guid MessageId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid AuthorId { get; set; }
    public required string Content { get; set; }
    public DateTime PostedAt { get; set; }
}
```

## Your Assignment: Two Tracks

### Track A: If you are building the Messaging Service

You are the **provider**. You publish `MessagePostedEvent`. Every other team depends on you getting this right.

#### The Feature

Implement `POST /api/messages`. It should:

1. Accept a request with `ChannelId`, `AuthorId`, and `Content`
2. Persist the message
3. Publish a `MessagePostedEvent` to the broker

```csharp
[HttpPost]
public async Task<IActionResult> PostMessage(CreateMessageRequest request)
{
    var message = Message.Create(request.ChannelId, request.AuthorId, request.Content);
    await _repository.Save(message);

    await _messageClient.PublishAsync(new MessagePostedEvent
    {
        MessageId = message.Id,
        ChannelId = message.ChannelId,
        AuthorId = message.AuthorId,
        Content = message.Content,
        PostedAt = message.PostedAt
    });

    return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, message);
}
```

#### The Tests

**Unit test: domain model**

```csharp
[Fact]
public void Message_Create_SetsAllFields()
{
    var channelId = Guid.NewGuid();
    var authorId = Guid.NewGuid();

    var message = Message.Create(channelId, authorId, "Hello world");

    message.Id.Should().NotBe(Guid.Empty);
    message.ChannelId.Should().Be(channelId);
    message.AuthorId.Should().Be(authorId);
    message.Content.Should().Be("Hello world");
    message.PostedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}

[Fact]
public void Message_Create_Throws_WhenContentIsEmpty()
{
    var act = () => Message.Create(Guid.NewGuid(), Guid.NewGuid(), "");
    act.Should().Throw<DomainException>();
}
```

**Provider contract test: verify what you publish**

This is your most important test. It verifies that the `MessagePostedEvent` you publish contains every field other services depend on, with correct values, not just that the type compiles.

```csharp
[Fact]
public async Task PostMessage_PublishedEvent_MeetsContract()
{
    var channelId = Guid.NewGuid();
    var authorId = Guid.NewGuid();
    var capture = new MessageCapture<MessagePostedEvent>(_factory.MessageClient);

    var response = await _client.PostAsJsonAsync("/api/messages", new
    {
        ChannelId = channelId,
        AuthorId = authorId,
        Content = "Hello world"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var evt = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));

    // Every field another service reads must be asserted here
    evt.MessageId.Should().NotBe(Guid.Empty);
    evt.ChannelId.Should().Be(channelId);
    evt.AuthorId.Should().Be(authorId);
    evt.Content.Should().Be("Hello world");
    evt.PostedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
}
```

**Integration test: full round trip**

```csharp
[Fact]
public async Task PostMessage_Returns201_AndPersistsMessage()
{
    var response = await _client.PostAsJsonAsync("/api/messages", new
    {
        ChannelId = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        Content = "Hello world"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var body = await response.Content.ReadFromJsonAsync<MessageDto>();
    body.Should().NotBeNull();
    body!.MessageId.Should().NotBe(Guid.Empty);

    // Verify it was actually persisted
    var get = await _client.GetAsync($"/api/messages/{body.MessageId}");
    get.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Track B: Everyone Else

You are a **consumer**. You react to `MessagePostedEvent` and do something meaningful in your own domain.

| Service       | What you do when a message is posted                             |
|---------------|------------------------------------------------------------------|
| Notifications | Detect mentions, publish `MentionDetectedEvent`                  |
| Presence      | Update author's last active timestamp                            |
| Search        | Index message content, publish `MessageIndexedEvent`             |
| Moderation    | Scan for banned content, publish `MessageFlaggedEvent` if needed |
| Analytics     | Increment channel message count                                  |
| Media         | Detect attachments, publish `AttachmentProcessedEvent`           |

> [!NOTE]
> The list is not exhaustive. If your domain isn't listed here or if you have other business logic to implement, feel free to do so.

#### The Feature

Add an `IMessageHandler<MessagePostedEvent>` to your service.

```csharp
public class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly IMessageClient _messageClient;

    public MessagePostedHandler(IMessageClient messageClient)
        => _messageClient = messageClient;

    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        // Do something domain-specific

        // Then publish your result, include MessageId for traceability
        await _messageClient.PublishAsync(new YourResultEvent
        {
            MessageId = message.MessageId,
            // ... your domain fields
            ProcessedAt = DateTime.UtcNow
        });
    }
}
```

Add your result event to `Shared.Contracts`. It must include `MessageId` so the message can be traced through the system.

#### The Tests

**Unit test: handler logic**

```csharp
[Fact]
public async Task Handle_PublishesResultEvent_WithCorrectMessageId()
{
    var messageId = Guid.NewGuid();
    var client = new FakeMessageClient();
    var handler = new MessagePostedHandler(client);

    await handler.Handle(new MessagePostedEvent
    {
        MessageId = messageId,
        ChannelId = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        Content = "Hello world",
        PostedAt = DateTime.UtcNow
    }, CancellationToken.None);

    var published = client.SinglePublished<YourResultEvent>();
    published.Should().NotBeNull();
    published!.MessageId.Should().Be(messageId);
}
```

**Consumer contract test: can your handler survive a valid message?**

This verifies your handler processes the minimum valid `MessagePostedEvent` without throwing. If the messaging team changes the event and these tests fail, you know the contract is broken before it reaches production.

```csharp
[Fact]
public async Task Handler_CanConsume_MinimumValidContract()
{
    var client = new FakeMessageClient();
    var handler = new MessagePostedHandler(client);

    // The minimum fields your handler depends on
    Func<Task> act = () => handler.Handle(new MessagePostedEvent
    {
        MessageId = Guid.NewGuid(),
        ChannelId = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        Content = "Hello world",
        PostedAt = DateTime.UtcNow
    }, CancellationToken.None);

    await act.Should().NotThrowAsync();
    client.Published.Should().HaveCount(1);
}
```

**Integration test: real broker, real handler**

```csharp
[Fact]
public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
{
    var messageId = Guid.NewGuid();
    var capture = new MessageCapture<YourResultEvent>(_factory.MessageClient);

    // Publish directly to the broker: no HTTP, tests the message path only
    await _factory.MessageClient.PublishAsync(new MessagePostedEvent
    {
        MessageId = messageId,
        ChannelId = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        Content = "Hello world",
        PostedAt = DateTime.UtcNow
    });

    var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));
    result.MessageId.Should().Be(messageId);
}
```

## A Note on Contracts

The shared project enforces the **shape** of a message at compile time. If the messaging team removes a field, your service won't compile. That's good.

But it doesn't verify the values. A field can be present and still be `Guid.Empty`, `null`, or `DateTime.MinValue`. That's what contract tests catch.

Provider contract tests (Track A) say: *"I promise to publish these values."* Consumer contract tests (Track B) say: *"I need these values to do my job."*

If both sides agree, the contract is valid. If either side changes without updating the other, a test fails, in CI, not in production.
