# Bizcord Web

The web frontend (ASP.NET Core Razor Pages). It talks **only to the API gateway** – never to a service directly.

Brought over from the BizcordShadow project and adapted to Bizcord's API. The UI text is Danish.

## Run

With the rest of the stack, from the repo root:

```bash
docker compose up -d --build
```

Open http://localhost:8084. Pick a test user in the top-right dropdown – Alice, Bob, Charlie and Dave match the
ChatService seed data and already have chats.

On its own (the gateway must be running on :8080):

```bash
dotnet run --project apps/web/src/Bizcord
```

The gateway address is `GatewayApi:BaseUrl` (compose sets `GatewayApi__BaseUrl=http://api-gateway:8080/`).

> The gateway reads its JWT signing key from Vault at startup, so Vault has to be initialised, unsealed and hold
> `kv/secrets` → `JwtSigningKey`, and the gateway needs `Vault__Token`. Without that, the gateway stops at startup and the
> frontend shows "Kunne ikke få forbindelse til API-gatewayen".

## How it calls the gateway

- **Login:** there is no UserAuth service yet. The chosen test user is kept in a cookie, and `GatewayClient` fetches a JWT for
  that user from the gateway's dev faucet (`POST /Faucet`, claims `sub` = user id, `role` = member). The token is cached
  per user for an hour and replaced once if the gateway answers 401.
- **Requests:** the token is sent as `Authorization: Bearer`; the gateway turns `sub` into ChatService's `X-User-Id`.
- **No shared code:** the response shapes are the frontend's own records (`Services/ChatApiModels.cs`) with only the fields
  it reads – a tolerant reader of [ChatService's contract](../chat-microservice/docs/contracts.md).

| Page action | Gateway call |
|---|---|
| Open a chat | `GET /chats/{chatId}` + `GET /chats/{chatId}/messages?limit=31` |
| Older messages | `GET /chats/{chatId}/messages?before={oldestId}&limit=31` |
| New chat (no name, one member) | `POST /chats/direct` |
| New chat (name) | `POST /chats/group`, then `POST /chats/{chatId}/participants` per member |
| Add participant | `POST /chats/{chatId}/participants` |
| Send / edit / delete message | `POST /chats/{chatId}/messages`, `PUT`/`DELETE /chats/{chatId}/messages/{messageId}` |

ChatService returns messages newest first; the client asks for one more than the page size to know whether older
messages exist, and reverses the page so the chat reads top to bottom.

## Known gaps

- **Chat list:** ChatService has no "my chats" endpoint yet. Until it does, `KnownChats` holds the seed chat ids plus the chats
  opened or created through this frontend, and the list asks for each of them (403 = not yours). The list is in memory, so
  chats created through the frontend drop out of it when the web container restarts – they still exist in ChatService.
- Rename chat, leave chat and unread counts are not in the UI, because ChatService has no endpoints for them.
- Attachments are shown by name and size; downloading needs a gateway route to FileService.
- No live updates yet – the page shows new messages on reload. RTC/SignalR would push them.

## Tests

```bash
dotnet test apps/web/tests/Bizcord.Web.Tests
```

`GatewayClient` is tested against a fake gateway (paging, ordering, faucet token, 401 retry, errors).
