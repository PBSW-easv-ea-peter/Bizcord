# Saga Workflow Scenarios

Pick a scenario that matches your domain and implement both the successful transactions and the compensating transactions. The scenarios below are intentionally messy - real business workflows are not clean, and the interesting part is figuring out what "undo" actually means for each step.

## Contents

- [Saga Workflow Scenarios](#saga-workflow-scenarios)
    - [Things to keep in mind](#things-to-keep-in-mind)
    - [The Scenarios](#the-scenarios)
        - [A user gets banned mid-conversation](#a-user-gets-banned-mid-conversation)
        - [A user sends a file that turns out to be too large](#a-user-sends-a-file-that-turns-out-to-be-too-large)
        - [A workspace is deleted by its last admin](#a-workspace-is-deleted-by-its-last-admin)
        - [A user purchases a premium subscription while their payment method is expiring](#a-user-purchases-a-premium-subscription-while-their-payment-method-is-expiring)
        - [A user is transferred to a different workspace by an administrator](#a-user-is-transferred-to-a-different-workspace-by-an-administrator)
- [Implement the Saga Pattern Using Your Message Client](#implement-the-saga-pattern-using-your-message-client)
    - [Scenario](#scenario)
    - [Before You Write Code - Design First](#before-you-write-code---design-first)
    - [Define Your Messages](#define-your-messages)
    - [Saga State](#saga-state)
    - [Choose Your Approach](#choose-your-approach)
        - [Option A: Orchestration](#option-a-orchestration)
        - [Option B: Choreography](#option-b-choreography)
    - [Justify Your Choice](#justify-your-choice)

## Things to keep in mind

Not every step can be compensated perfectly. A sent email cannot be unsent. A notification that already fired cannot be unfired. Part of your design is deciding what to do when a compensating transaction is itself impossible - do you log it, alert an operator, or accept the inconsistency?

## The Scenarios
### A user gets banned mid-conversation

A moderator bans a user who is currently active in several channels and an ongoing voice call.

- **User Management:** Mark user as banned + invalidate all active sessions
    - Compensate: Unban user, restore session access
- **Channel:** Forcibly remove user from all channels they are in, post a system message that the user was removed
    - Compensate: Re-add user to channels, delete system messages
- **Streaming / VOIP:** Terminate any active voice or video sessions immediately
    - Compensate: Streaming access restored - but the call is already gone. What do you tell the other participants?

**The hard question:** If the ban succeeds but the voice termination fails, is the user banned? What state is the system in?

### A user sends a file that turns out to be too large

A user uploads a large file and attaches it to a message in a busy channel. Halfway through, it turns out they have exceeded their storage quota.

- **File Sharing:** Accept file upload, store temporarily pending quota check
    - Compensate: Delete the temporarily stored file
- **Storage / Quota:** Validate that the user has enough quota, deduct the space
    - Compensate: Restore quota, reject the file
- **Message:** Insert message with the file attachment link into the channel
    - Compensate: Delete the message - but some users may have already seen it
- **Channel:** Update last activity timestamp and unread counters for all members
    - Compensate: Revert activity timestamp and unread counters

**The hard question:** If the message was already delivered to 200 online users before the quota check failed, what does compensation actually look like?

### A workspace is deleted by its last admin

The last admin of a workspace deletes it. The workspace has active channels, ongoing voice calls, stored files, and members who have not been warned.

- **User Management:** Notify all members that the workspace will be deleted in 60 seconds
    - Compensate: Send a follow-up message that the deletion was cancelled
- **Streaming / VOIP:** Terminate all active calls in the workspace
    - Compensate: Calls are gone - participants need to be notified manually
- **Channel:** Archive all channels and their message history
    - Compensate: Restore channels from archive
- **File Sharing:** Mark all workspace files for deletion, begin purge
    - Compensate: Cancel purge if it has not yet completed - what if it has?
- **Notification:** Send each member a final "workspace deleted" notification
    - Compensate: No compensation possible - the notification was sent

**The hard question:** File purge is async and may take minutes. How do you compensate a step that is still in progress when a later step fails?

### A user purchases a premium subscription while their payment method is expiring

A user upgrades to premium. Their card processes but then the payment provider reports a delayed failure. By that time, premium features are already active.

- **Payment:** Charge the card, receive initial success response
    - Compensate: Issue a refund when the delayed failure arrives
- **Subscription:** Activate premium features immediately on payment success
    - Compensate: Deactivate premium - user may have already used premium features
- **Profile:** Add premium badge, update visible status indicators
    - Compensate: Remove badge and indicators
- **Notification:** Send "Welcome to Premium" email and in-app notification
    - Compensate: Send "We encountered an issue" follow-up - the welcome message cannot be unsent

**The hard question:** The delayed failure arrives 10 minutes after success. The user has already used their premium features. How do you handle compensation that happens long after the saga appeared to complete?

### A user is transferred to a different workspace by an administrator

An admin moves a user from one workspace to another. The user is currently online, has pinned messages, custom notification rules, and is an admin in several channels.

- **User Management:** Revoke workspace membership, grant membership in new workspace
    - Compensate: Revert both membership changes
- **Channel:** Remove user from all channels in old workspace, assign them to default channels in new workspace
    - Compensate: Restore channel membership in old workspace, remove from new
- **Notification:** Migrate custom notification rules to the new workspace, archive old rules
    - Compensate: Restore old rules, discard migrated rules
- **Message:** Transfer pinned messages and bookmarks or archive them
    - Compensate: Restore pinned messages in original location
- **Streaming / VOIP:** If user is in an active call, warn them and disconnect after a grace period
    - Compensate: Grace period has not yet expired - cancel the disconnect

**The hard question:** The user is in the middle of the transfer. They are seeing a partially migrated state. How do you communicate to them what is happening?

# Implement the Saga Pattern Using Your Message Client

The objective of this assignment is to implement the Saga pattern using the `MessageClient` you already have in your solution. There is no saga framework here - you will implement the coordination logic yourself, which means you will understand exactly what a framework like Rebus is doing under the hood.

## Scenario

Pick a multi-step workflow from your domain that spans more than one service. I've provided you with a list of scenarios in the [Saga Workflow Scenarios](#saga-workflow-scenarios).

## Before You Write Code - Design First

Draw the happy path and the failure paths for your saga.

For every step, answer:

- What message starts this step?
- What message does it emit on success?
- What message does it emit on failure?
- What is the compensating action if a later step fails?
```
Step 1: Charge card
  Started by:    InitiatePurchase
  Success:       PaymentSucceeded
  Failure:       PaymentFailed  (saga ends, nothing to compensate yet)
  Compensation:  RefundPayment  (triggered if step 2 or 3 fails)

Step 2: Activate account
  Started by:    PaymentSucceeded
  Success:       AccountActivated
  Failure:       AccountActivationFailed  (must compensate step 1)
  Compensation:  DeactivateAccount  (triggered if step 3 fails)

Step 3: Send email
  Started by:    AccountActivated
  Success:       EmailSent  (saga complete)
  Failure:       EmailFailed  (compensate step 1 and 2)
  Compensation:  none - email already sent cannot be unsent
```

## Define Your Messages

Every transition in the saga is a message. Define them in `Shared.Contracts`.
```c#
// Commands: tell a service to do something
public class InitiatePurchase
{
    public Guid SagaId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

public class RefundPayment
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

// Events: something happened
public class PaymentSucceeded
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentFailed
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = null!;
}

public class AccountActivated
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
}

public class AccountActivationFailed
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = null!;
}
```
>SagaId is not optional

>Every message must carry a `SagaId`. This is how you correlate messages back to the same saga instance. Without it, you cannot tell which purchase a `PaymentSucceeded` event belongs to.

## Saga State

The saga needs to remember what has happened so far. Each saga instance is identified by its `SagaId` and persists its state between messages.
```c#
// In your chosen service: the one coordinating the flow
public class PurchaseSagaState
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }

    public bool PaymentProcessed { get; set; }
    public bool AccountActivated { get; set; }
    public bool EmailSent { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```
```c#
// Persist saga state between messages
public interface ISagaStateRepository
{
    Task<PurchaseSagaState?> GetById(Guid sagaId);
    Task Save(PurchaseSagaState state);
}
```
>Why do you need to persist state?

>Your handlers are stateless - they process one message and exit. The saga state is what connects them. When `PaymentSucceeded` arrives, your handler loads the state, marks payment as done, and decides what to do next.

## Choose Your Approach
### Option A: Orchestration

One handler owns the entire flow. It receives every message in the saga and decides what happens next. All saga logic lives in one place.
```c#
// The orchestrator handles every message in the saga
public class PurchaseSagaOrchestrator :
    IMessageHandler<InitiatePurchase>,
    IMessageHandler<PaymentSucceeded>,
    IMessageHandler<PaymentFailed>,
    IMessageHandler<AccountActivated>,
    IMessageHandler<AccountActivationFailed>
{
    private readonly IMessageClient _messageClient;
    private readonly ISagaStateRepository _sagaRepo;

    public PurchaseSagaOrchestrator(
        IMessageClient messageClient,
        ISagaStateRepository sagaRepo)
    {
        _messageClient = messageClient;
        _sagaRepo = sagaRepo;
    }

    public async Task Handle(InitiatePurchase message, CancellationToken ct)
    {
        // Create a new saga instance
        var state = new PurchaseSagaState
        {
            SagaId = message.SagaId,
            UserId = message.UserId,
            Amount = message.Amount
        };
        await _sagaRepo.Save(state);

        // Step 1: tell PaymentService to charge the card
        await _messageClient.PublishAsync(new ChargeCard
        {
            SagaId = message.SagaId,
            UserId = message.UserId,
            Amount = message.Amount
        });
    }

    public async Task Handle(PaymentSucceeded message, CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);
        if (state is null) return;

        state.PaymentProcessed = true;
        await _sagaRepo.Save(state);

        // Step 2: tell SubscriptionService to activate the account
        await _messageClient.PublishAsync(new ActivateAccount
        {
            SagaId = message.SagaId,
            UserId = message.UserId
        });
    }

    public async Task Handle(PaymentFailed message, CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);
        if (state is null) return;

        // Nothing to compensate yet - saga ends here
        state.IsFailed = true;
        await _sagaRepo.Save(state);
    }

    public async Task Handle(AccountActivated message, CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);
        if (state is null) return;

        state.AccountActivated = true;
        await _sagaRepo.Save(state);

        // Step 3: tell NotificationService to send confirmation
        await _messageClient.PublishAsync(new SendConfirmationEmail
        {
            SagaId = message.SagaId,
            UserId = message.UserId
        });
    }

    public async Task Handle(AccountActivationFailed message, CancellationToken ct)
    {
        var state = await _sagaRepo.GetById(message.SagaId);
        if (state is null) return;

        state.IsFailed = true;
        await _sagaRepo.Save(state);

        // Compensate step 1 - refund the card
        await _messageClient.PublishAsync(new RefundPayment
        {
            SagaId = message.SagaId,
            UserId = message.UserId,
            Amount = state.Amount
        });
    }
}
```
### Option B: Choreography

No central coordinator. Each service reacts to the previous event and emits the next one. The flow is implicit in the chain of events.
```c#
// PaymentService handles the initial command
public class ChargeCardHandler : IMessageHandler<InitiatePurchase>
{
    private readonly IMessageClient _messageClient;
    private readonly IPaymentGateway _gateway;

    public ChargeCardHandler(IMessageClient messageClient, IPaymentGateway gateway)
    {
        _messageClient = messageClient;
        _gateway = gateway;
    }

    public async Task Handle(InitiatePurchase message, CancellationToken ct)
    {
        try
        {
            await _gateway.Charge(message.UserId, message.Amount);

            await _messageClient.PublishAsync(new PaymentSucceeded
            {
                SagaId = message.SagaId,
                UserId = message.UserId,
                Amount = message.Amount
            });
        }
        catch (Exception ex)
        {
            await _messageClient.PublishAsync(new PaymentFailed
            {
                SagaId = message.SagaId,
                UserId = message.UserId,
                Reason = ex.Message
            });
        }
    }
}

// SubscriptionService reacts to PaymentSucceeded
public class ActivateAccountHandler : IMessageHandler<PaymentSucceeded>
{
    private readonly IMessageClient _messageClient;
    private readonly ISubscriptionRepository _repo;

    public ActivateAccountHandler(IMessageClient messageClient, ISubscriptionRepository repo)
    {
        _messageClient = messageClient;
        _repo = repo;
    }

    public async Task Handle(PaymentSucceeded message, CancellationToken ct)
    {
        try
        {
            await _repo.Activate(message.UserId);

            await _messageClient.PublishAsync(new AccountActivated
            {
                SagaId = message.SagaId,
                UserId = message.UserId
            });
        }
        catch (Exception ex)
        {
            await _messageClient.PublishAsync(new AccountActivationFailed
            {
                SagaId = message.SagaId,
                UserId = message.UserId,
                Reason = ex.Message
            });
        }
    }
}

// PaymentService also listens for AccountActivationFailed to refund
public class RefundPaymentHandler : IMessageHandler<AccountActivationFailed>
{
    private readonly IMessageClient _messageClient;
    private readonly IPaymentGateway _gateway;

    public RefundPaymentHandler(IMessageClient messageClient, IPaymentGateway gateway)
    {
        _messageClient = messageClient;
        _gateway = gateway;
    }

    public async Task Handle(AccountActivationFailed message, CancellationToken ct)
    {
        // Compensating transaction - refund the card charged in the previous step
        await _gateway.Refund(message.UserId);

        await _messageClient.PublishAsync(new PaymentRefunded
        {
            SagaId = message.SagaId,
            UserId = message.UserId
        });
    }
}
```
## Justify Your Choice

Whatever approach you pick, write a short paragraph explaining:

- Why did you choose orchestration or choreography for your domain?
- What would be harder with the other approach?
- Where does the saga state live, and why there?
