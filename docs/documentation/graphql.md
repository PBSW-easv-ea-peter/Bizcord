# GraphQL presentation layer – testing and design choices

Week 47: a GraphQL layer (HotChocolate) in ChatService, next to REST, on top of the same application services (`ChatAppService`, `MessageAppService`). Code: `apps/chat-microservice/src/ChatService/GraphQL/`.

## Contents

- [Endpoint and Nitro](#endpoint-and-nitro)
- [Identifying the caller: `X-User-Id`](#identifying-the-caller-x-user-id)
- [Test data: where `chatId` and `messageId` come from](#test-data-where-chatid-and-messageid-come-from)
- [Test scenarios](#test-scenarios)
- [Error codes](#error-codes)
- [Schema](#schema)
- [Design choices](#design-choices)

## Endpoint and Nitro

| | URL |
|---|---|
| Docker (`docker compose up -d --build`) | `http://localhost:8081/graphql` |
| Schema as SDL | `http://localhost:8081/graphql?sdl` |

Open the endpoint in a browser to get **Nitro**, HotChocolate's GraphQL IDE. Nitro has three panes you need:

- **Operation** – the query or mutation.
- **Variables** – a JSON object with the values for `$chatId` etc.
- **Headers** – a JSON object, see below.

The schema can be browsed without a header (introspection does not need a caller).

Every request is an HTTP `POST` with a JSON body `{ "query": "...", "variables": { ... } }`. The response is always HTTP 200 – errors are reported in the `errors` array, not as status codes.

## Identifying the caller: `X-User-Id`

Like REST, GraphQL reads the caller from the `X-User-Id` header (until JWT replaces it – then only `UserIdInterceptor.cs` changes). The test users from `apps/chat-microservice/src/ChatService/ChatService.http`:

| User | Id |
|---|---|
| alice | `11111111-1111-1111-1111-111111111111` |
| bob | `22222222-2222-2222-2222-222222222222` |

In Nitro's **Headers** pane:

```json
{ "X-User-Id": "11111111-1111-1111-1111-111111111111" }
```

With curl: `-H "X-User-Id: 11111111-1111-1111-1111-111111111111"`.

A missing or invalid header gives `UNAUTHENTICATED`. If you get that with a header set, check the value for typos and spaces – it must parse as a Guid.

## Test data: where `chatId` and `messageId` come from

GraphQL cannot create chats or add participants (deliberately – see [Design choices](#design-choices)). Create them with REST, either with the first three requests in `ChatService.http` (set `@host = http://localhost:8081`) or with curl:

```bash
# 1. Alice creates a group – copy "id" from the response: that is the chatId
curl -s -X POST http://localhost:8081/chats/group \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 11111111-1111-1111-1111-111111111111" \
  -d '{"title":"GraphQL test"}'

# 2. Alice adds Bob (optional – needed to test Bob as a participant)
curl -s -X POST http://localhost:8081/chats/<chatId>/participants \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 11111111-1111-1111-1111-111111111111" \
  -d '{"userId":"22222222-2222-2222-2222-222222222222"}'
```

Messages can then be sent with the `sendMessage` mutation below. The `messageId` used by edit and delete is the `id` in the `sendMessage` response (or any `id` from the `messages` field).

## Test scenarios

All examples use alice's header unless stated otherwise. Replace `<chatId>` and `<messageId>` with your own values.

### 1. Read a chat

```graphql
query Chat($chatId: UUID!) {
  chat(chatId: $chatId) {
    id
    type
    title
    participants { userId role }
  }
}
```

```json
{ "chatId": "<chatId>" }
```

Expected: `data.chat` with the chat and alice as `Owner`.

### 2. Send a message

```graphql
mutation Send($chatId: UUID!, $content: String!) {
  sendMessage(chatId: $chatId, content: $content) {
    id
    senderUserId
    content
    sentAt
  }
}
```

```json
{ "chatId": "<chatId>", "content": "Hej fra GraphQL" }
```

Expected: the new message. Copy `id` – it is the `messageId` for scenario 4 and 5. Send 3–5 messages to have something to paginate.

The mutation publishes `chat.message-sent` exactly like REST, so RTC pushes it to online participants other than the sender.

### 3. Read messages with pagination

`messages` is a field on the chat, so chat and messages come back in one request. Newest first.

```graphql
query ChatMessages($chatId: UUID!, $limit: Int, $before: UUID) {
  chat(chatId: $chatId) {
    id
    title
    messages(limit: $limit, before: $before) {
      id
      senderUserId
      content
      sentAt
      editedAt
      deletedAt
    }
  }
}
```

First page:

```json
{ "chatId": "<chatId>", "limit": 2 }
```

Next page – `before` is the `id` of the oldest message on the previous page:

```json
{ "chatId": "<chatId>", "limit": 2, "before": "<id of oldest message on page 1>" }
```

| Variables | Expected |
|---|---|
| no `limit` | up to 50 messages (default) |
| `"limit": 2` | the 2 newest |
| `"limit": 2, "before": ...` | the next 2, no overlap with page 1 |
| `"limit": 0` | 1 message (clamped to 1) |
| `"limit": 500` | at most 100 (clamped) |
| `"before"`: an unknown id | `NOT_FOUND` |

Try leaving `messages` out of the selection: the resolver does not run at all, and no messages are read from the database.

### 4. Edit a message

```graphql
mutation Edit($chatId: UUID!, $messageId: UUID!, $content: String!) {
  editMessage(chatId: $chatId, messageId: $messageId, content: $content) {
    id
    content
    editedAt
  }
}
```

```json
{ "chatId": "<chatId>", "messageId": "<messageId>", "content": "Rettet" }
```

Expected: the new `content` and `editedAt` set. Only the sender can edit.

### 5. Delete a message

```graphql
mutation Delete($chatId: UUID!, $messageId: UUID!) {
  deleteMessage(chatId: $chatId, messageId: $messageId)
}
```

```json
{ "chatId": "<chatId>", "messageId": "<messageId>" }
```

Expected: `{ "data": { "deleteMessage": "<messageId>" } }`. Deleting again returns the id again (idempotent). Afterwards the message is still in `messages`, but with `content: null` and `deletedAt` set (soft delete).

## Error codes

Errors are returned as `errors[].extensions.code`. Known domain errors are mapped by `DomainErrorFilter` (the GraphQL counterpart of REST's `DomainExceptionHandler`); anything else stays "Unexpected Execution Error" so internals don't leak.

| Code | REST equivalent | How to trigger it |
|---|---|---|
| `UNAUTHENTICATED` | 400 (missing header) | Any operation without `X-User-Id` |
| `FORBIDDEN` | 403 | Bob reads or sends in a chat without being a participant; Bob edits or deletes Alice's message |
| `NOT_FOUND` | 404 | Unknown `chatId`, or `before`/`messageId` not in the chat |
| `BAD_REQUEST` | 400 | `sendMessage` or `editMessage` with `"content": ""` |
| `CONFLICT` | 409 | `editMessage` on a deleted message |

Example – bob reading alice's chat (header `22222222-...`, scenario 1):

```json
{
  "errors": [
    {
      "message": "Only active participants can read the chat.",
      "path": ["chat"],
      "extensions": { "code": "FORBIDDEN" }
    }
  ],
  "data": null
}
```

Leaving out a required argument (e.g. `content`) is rejected by the schema before any resolver runs, with a validation error instead of a code.

## Schema

```graphql
type ChatQuery {
  chat(chatId: UUID!): ChatResponse!
}

type ChatMutation {
  sendMessage(chatId: UUID!, content: String!): MessageResponse!
  editMessage(chatId: UUID!, messageId: UUID!, content: String!): MessageResponse!
  deleteMessage(chatId: UUID!, messageId: UUID!): UUID!
}

type ChatResponse {
  id: UUID!
  type: String!
  title: String
  createdAt: DateTime!
  participants: [ParticipantResponse!]!
  messages(before: UUID, limit: Int! = 50): [MessageResponse!]!
}
```

Full SDL: `http://localhost:8081/graphql?sdl`.

## Design choices

**BFF-style layer next to REST, same application services.** The resolvers are as thin as the controllers: read the caller, call the application service, map to the contract records. Validation, authorization and event publishing stay in one place, so a message sent via GraphQL behaves exactly like one sent via REST.

**The caller is read once per request.** `UserIdInterceptor` puts the header value in global state; resolvers call `RequireUserId()`. A missing header is not an error in the interceptor, because schema introspection (Nitro) must work without a user.

**`messages` is a type extension, not a property on `ChatResponse`.** The REST contract is unchanged, and the message query only runs when the client selects the field – the client decides the shape of the response, which is the main point of GraphQL over REST.

**Cursor pagination with flat arguments (`before`, `limit`).** It mirrors the REST endpoint and the application service. HotChocolate's `[UsePaging]` (Relay connections) would give `edges`/`pageInfo`, but more ceremony than this lab needs.

**Flat mutation arguments instead of mutation conventions.** HotChocolate's `AddMutationConventions()` generates input and payload types and typed errors in the schema. That is the recommended style for a public API, but it adds a lot of types for three mutations.

**Only message mutations.** Creating chats and adding participants is left to REST. The UI need that GraphQL targets is reading a chat with its messages and writing messages.

**Null propagation.** `chat` and `messages` are non-null. If `messages` fails, the null propagates up and the whole `data` becomes `null`, so the client also loses the chat. Making `messages` nullable would let the client keep the chat when only the messages fail. We accept the current behaviour because both use the same authorization check.

**N+1 is not a problem yet.** There is only a single-chat query, so `messages` runs once per request. A future `chats` query returning many chats would run one message query per chat (N+1). HotChocolate solves that with DataLoaders, which batch the lookups into one query.

**A real setup would put GraphQL at the edge.** Here GraphQL lives inside ChatService, so it can only shape ChatService data. In production the usual setup is a GraphQL gateway (e.g. HotChocolate Fusion) that composes the schemas of several services, or a BFF per client type (web, mobile) that aggregates REST calls to several services. The current design moves there easily, because the resolvers only depend on the application services.
