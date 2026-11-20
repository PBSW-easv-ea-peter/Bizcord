using System.Net;
using Bizcord.Models;
using Bizcord.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bizcord.Pages;

public class ChatModel(GatewayClient gateway, KnownChats knownChats, CurrentUser currentUser) : PageModel
{
    public string UserName => currentUser.Name;
    public Guid UserId => currentUser.Id;

    /// <summary>The chat list on the left, newest activity first.</summary>
    public IReadOnlyList<ChatPreview> Chats { get; private set; } = [];

    /// <summary>Set if the list could not be loaded (e.g. the gateway is not running).</summary>
    public string? ChatsError { get; private set; }

    /// <summary>Everyone who can be added (all test users except yourself).</summary>
    public IReadOnlyList<TestUser> OtherUsers => TestUsers.All.Where(u => u.Id != currentUser.Id).ToList();

    /// <summary>The open chat from the URL (/Chat/{id}). Null = no chat open, so the welcome is shown.</summary>
    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid? OpenChatId { get; set; }

    public ChatDto? OpenChat { get; private set; }

    public string OpenChatName => OpenChat is null ? string.Empty : ChatPreview.NameOf(OpenChat, currentUser.Id);

    public IReadOnlyList<ParticipantDto> OpenChatMembers => OpenChat?.ActiveParticipants.ToList() ?? [];

    /// <summary>Set if the open chat could not be loaded (e.g. 403).</summary>
    public string? OpenChatError { get; private set; }

    /// <summary>The newest messages in the open chat. Older ones are loaded with "Vis ældre beskeder" (OnGetOlderMessages).</summary>
    public MessageList? Messages { get; private set; }

    /// <summary>Set if the messages could not be loaded (the chat is shown anyway).</summary>
    public string? MessagesError { get; private set; }

    /// <summary>A message from the previous request (e.g. "Chat oprettet") that survives the redirect.</summary>
    [TempData]
    public string? FlashMessage { get; set; }

    /// <summary>
    /// New chat: an empty name and exactly one member gives a direct chat, otherwise a group chat with that name.
    /// The name can be left empty for a group to try ChatService's validation.
    /// </summary>
    [BindProperty]
    public string NewChatName { get; set; } = string.Empty;

    [BindProperty]
    public List<Guid> NewChatMemberIds { get; set; } = [];

    /// <summary>The chat participants are added to. The open chat is used if there is one; otherwise an id can be pasted.</summary>
    [BindProperty]
    public Guid? ChatId { get; set; }

    [BindProperty]
    public string? ChatName { get; set; }

    [BindProperty]
    public Guid NewParticipantUserId { get; set; }

    /// <summary>The text in the composer. Empty or only spaces is not sent ("." is fine).</summary>
    [BindProperty]
    public string? NewMessage { get; set; }

    public string? StatusMessage { get; private set; }
    public bool IsError { get; private set; }

    /// <summary>Where the error from a failed POST is shown: the "create" or "add" dialog, or the composer ("message").</summary>
    public string? OpenDialog { get; private set; }

    public async Task OnGet() => await LoadPageAsync();

    /// <summary>
    /// GET /Chat/{id}?handler=OlderMessages&amp;before=... : one page of older messages as HTML.
    /// Called from JavaScript when "Vis ældre beskeder" is pressed, and inserted at the top of the list.
    /// </summary>
    public async Task<IActionResult> OnGetOlderMessages(Guid before)
    {
        if (OpenChatId is null)
        {
            return BadRequest();
        }

        var result = await gateway.GetMessagesAsync(currentUser.Id, OpenChatId.Value, before);
        if (!result.IsSuccess)
        {
            return new ContentResult { StatusCode = StatusCodes.Status502BadGateway, Content = result.Error, ContentType = "text/plain; charset=utf-8" };
        }

        return Partial("_ChatMessages", new MessageList(result.Value!.Messages, result.Value.HasOlder, currentUser.Id, OpenChatId.Value));
    }

    public async Task<IActionResult> OnPostCreateChat()
    {
        OpenDialog = "create";

        if (NewChatMemberIds.Count == 0)
        {
            SetError("Vælg mindst ét medlem. En chat kun med dig selv er ikke så interessant.");
            await LoadPageAsync();
            return Page();
        }

        var isDirect = string.IsNullOrWhiteSpace(NewChatName) && NewChatMemberIds.Count == 1;
        var created = isDirect
            ? await gateway.CreateDirectChatAsync(currentUser.Id, NewChatMemberIds[0])
            : await gateway.CreateGroupChatAsync(currentUser.Id, NewChatName ?? string.Empty);

        if (!created.IsSuccess)
        {
            SetError($"Kunne ikke oprette chat. {created.Error}");
            await LoadPageAsync();
            return Page();
        }

        var chat = created.Value!;
        knownChats.Add(chat.Id);

        // ChatService creates a group with only its owner, so the members are added one by one.
        var failed = new List<string>();
        if (!isDirect)
        {
            foreach (var memberId in NewChatMemberIds)
            {
                var added = await gateway.AddParticipantAsync(currentUser.Id, chat.Id, memberId);
                if (!added.IsSuccess)
                {
                    failed.Add($"{TestUsers.NameOf(memberId)} ({added.Error})");
                }
            }
        }

        var name = ChatPreview.NameOf(chat, currentUser.Id);
        FlashMessage = failed.Count == 0
            ? $"Chat \"{name}\" er klar."
            : $"Chat \"{name}\" er oprettet, men disse kunne ikke tilføjes: {string.Join(", ", failed)}";

        // Redirect after POST, so F5 does not create the chat again
        return RedirectToPage("/Chat", new { id = chat.Id });
    }

    public async Task<IActionResult> OnPostAddParticipant()
    {
        OpenDialog = "add";

        if (ChatId is null)
        {
            SetError("Angiv hvilken chat (opret en chat først, eller indsæt et chat-id).");
            await LoadPageAsync();
            return Page();
        }

        var name = TestUsers.NameOf(NewParticipantUserId);
        var result = await gateway.AddParticipantAsync(currentUser.Id, ChatId.Value, NewParticipantUserId);

        if (!result.IsSuccess)
        {
            SetError($"Kunne ikke tilføje {name}. {result.Error}");
            await LoadPageAsync();
            return Page();
        }

        knownChats.Add(ChatId.Value);
        FlashMessage = $"{name} er tilføjet til chatten.";
        return RedirectToPage("/Chat", new { id = ChatId });
    }

    public async Task<IActionResult> OnPostEditMessage(Guid messageId, string? editedContent)
    {
        if (OpenChatId is null)
        {
            return RedirectToPage("/Chat");
        }

        // No checks here: ChatService validates the text (400) and that you are the sender (403)
        var result = await gateway.EditMessageAsync(currentUser.Id, OpenChatId.Value, messageId, editedContent ?? string.Empty);

        if (!result.IsSuccess)
        {
            OpenDialog = "message";
            SetError($"Kunne ikke redigere beskeden. {result.Error}");
            await LoadPageAsync();
            return Page();
        }

        return RedirectToPage("/Chat", new { id = OpenChatId });
    }

    public async Task<IActionResult> OnPostDeleteMessage(Guid messageId)
    {
        if (OpenChatId is null)
        {
            return RedirectToPage("/Chat");
        }

        var result = await gateway.DeleteMessageAsync(currentUser.Id, OpenChatId.Value, messageId);

        if (!result.IsSuccess)
        {
            OpenDialog = "message";
            SetError($"Kunne ikke slette beskeden. {result.Error}");
            await LoadPageAsync();
            return Page();
        }

        return RedirectToPage("/Chat", new { id = OpenChatId });
    }

    public async Task<IActionResult> OnPostSendMessage()
    {
        OpenDialog = "message";

        if (OpenChatId is null)
        {
            return RedirectToPage("/Chat");
        }

        // The send button is disabled when the field is empty, but JavaScript may be off
        if (string.IsNullOrWhiteSpace(NewMessage))
        {
            SetError("Beskeden er tom.");
            await LoadPageAsync();
            return Page();
        }

        var result = await gateway.SendMessageAsync(currentUser.Id, OpenChatId.Value, NewMessage);

        if (!result.IsSuccess)
        {
            // Keeps the text in the field, so it is not lost
            SetError($"Kunne ikke sende beskeden. {result.Error}");
            await LoadPageAsync();
            return Page();
        }

        return RedirectToPage("/Chat", new { id = OpenChatId });
    }

    private async Task LoadPageAsync()
    {
        await LoadChatsAsync();
        await LoadOpenChatAsync();
    }

    // TEMPORARY until ChatService has GET /chats: ask for every known chat and keep the ones the user is in.
    private async Task LoadChatsAsync()
    {
        var chats = await Task.WhenAll(knownChats.All.Select(id => gateway.GetChatAsync(currentUser.Id, id)));

        if (chats.Any(c => !c.IsSuccess && c.StatusCode is null))
        {
            ChatsError = GatewayClient.ConnectionError;
            return;
        }

        // 403/404 just means "not your chat" - anything else is worth showing
        var unexpected = chats.FirstOrDefault(c => !c.IsSuccess && c.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.NotFound));
        if (unexpected is not null)
        {
            ChatsError = unexpected.Error;
            return;
        }

        var mine = chats
            .Where(c => c.IsSuccess && c.Value!.HasActiveParticipant(currentUser.Id))
            .Select(c => c.Value!)
            .ToList();

        var previews = await Task.WhenAll(mine.Select(async chat =>
        {
            var latest = await gateway.GetMessagesAsync(currentUser.Id, chat.Id, limit: 1);
            return ChatPreview.From(chat, latest.IsSuccess ? latest.Value!.Messages.LastOrDefault() : null, currentUser.Id);
        }));

        Chats = previews.OrderByDescending(p => p.LastActivity).ToList();
    }

    // The chat from the URL, and prefill "Tilføj deltager" with it
    private async Task LoadOpenChatAsync()
    {
        if (OpenChatId is null)
        {
            return;
        }

        var result = await gateway.GetChatAsync(currentUser.Id, OpenChatId.Value);
        if (!result.IsSuccess)
        {
            OpenChatError = result.Error;
            return;
        }

        OpenChat = result.Value!;
        knownChats.Add(OpenChat.Id);

        var messages = await gateway.GetMessagesAsync(currentUser.Id, OpenChatId.Value);
        if (messages.IsSuccess)
        {
            Messages = new MessageList(messages.Value!.Messages, messages.Value.HasOlder, currentUser.Id, OpenChatId.Value);
        }
        else
        {
            MessagesError = messages.Error;
        }

        // Only if the user has not posted another chat id
        if (ChatId is null)
        {
            ChatId = OpenChat.Id;
            ChatName = OpenChatName;
        }
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        IsError = true;
    }
}
