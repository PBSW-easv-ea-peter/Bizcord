workspace "Bizcord" "C4 model - Level 2 (Container diagram) and Level 3 (Components for ChatService and RTC)" {

    model {
        user = person "Bizcord User" "A user of Bizcord interacting with multiple channels."

        bizcord = softwareSystem "Bizcord" "Chat platform with channels, messaging and engagement features." {

            client = container "Bizcord Client" "Client application used by end users." "" "Client App"

            group "UserAuth" {
                userAuthService = container "UserAuth Service" "Handles login by verifying login-information, resetting passwords and disabling users." "" "Service"
                authDb = container "AuthDB" "User information: password, email, login_attempts." "RDBMS" "Database"
            }

            group "User" {
                userService = container "User Service" "Handles profile-data, subscribed channels and friend lists." "" "Service"
                userDb = container "UserDB" "Contains user information: username, channels and friends." "RDBMS" "Database"
            }

            group "Channel" {
                channelService = container "ChannelService" "Manages servers and channels." "" "Service"
                channelDb = container "ChannelDB" "Contains channel information: name, users, server, channel, message." "RDBMS" "Database"
            }

            group "Chat" {
                chatService = container "ChatService" "Handles direct and group chats: participants, messages (send/edit/delete) and read receipts (delivered/seen). Source of truth via REST." "ASP.NET Core Web API (Dapper, EasyNetQ)" "Service" {
                    chatsController = component "ChatsController" "REST: POST /chats/direct, POST /chats/group, GET /chats/{id}, POST /chats/{id}/participants. Caller identity from the X-User-Id header (MVP)." "ASP.NET Core Web API Controller"
                    chatMessagesController = component "MessagesController" "REST under /chats/{id}/messages: send (POST), page (GET), edit (PUT), delete (DELETE), mark seen (POST .../seen), receipts (GET .../receipts)." "ASP.NET Core Web API Controller"
                    chatAppService = component "ChatAppService" "Use cases for chats: create direct/group chat, get chat, add participant. Publishes chat.participant-added after save." "C# application service"
                    messageAppService = component "MessageAppService" "Use cases for messages and receipts. Saves first, then publishes chat.message-sent/-edited/-deleted and chat.messages-seen. Sets delivered_at from rtc.message-delivered." "C# application service"
                    domainModel = component "Domain model" "Chat (aggregate root), ChatParticipant, Message, MessageReceipt and value objects. Holds the rules for who may send, edit, delete and add participants." "C# domain classes"
                    repositories = component "Repositories" "IChatRepository, IMessageRepository and IReceiptRepository, implemented with Dapper over Npgsql." "C# / Dapper"
                    chatMessageClient = component "IMessageClient" "Abstraction over the message broker (Publish/Subscribe), so the application layer does not depend on EasyNetQ." "C# Interface"
                    chatRabbitMqMessageClient = component "RabbitMqMessageClient" "EasyNetQ-based implementation of IMessageClient. Uses logical event names (EventTypeNames) instead of C# type names." "C# / EasyNetQ"
                    messageDeliveredConsumer = component "MessageDeliveredConsumer" "Subscribes to rtc.message-delivered on startup and calls MessageAppService in a new DI scope per message." "ASP.NET Core IHostedService"
                }
                chatDb = container "ChatDB" "Contains chats, participants, messages and message receipts." "PostgreSQL" "Database"
            }

            group "Engagement" {
                engagementService = container "Engagement Service" "Handles likes, reactions etc." "" "Service"
                engagementDb = container "EngagementDB" "Contains engagement information: server_id, message_id, user_id, reaction." "RDBMS" "Database"
            }

            group "RealTimeCommunication" {
                realTimeCommunicationService = container "Real-Time Communication Service" "Subscribes to domain events via RabbitMQ (EasyNetQ) and pushes real-time updates/notifications to clients." "ASP.NET Core Web API (EasyNetQ)" "Event-hub" {
                    rtcMessagesController = component "MessagesController" "Manual test endpoint (POST /api/Messages) used to verify the publish pipeline. Not a finalized part of the domain API - more/other endpoints may be added later." "ASP.NET Core Web API Controller"
                    rtcMessageClient = component "IMessageClient" "Abstraction over the message broker (Publish/Subscribe), decouples the service from a specific broker technology so it can be swapped later (e.g. Kafka)." "C# Interface"
                    rtcRabbitMqMessageClient = component "RabbitMqMessageClient" "EasyNetQ-based implementation of IMessageClient that talks to RabbitMQ. Maps logical event names (e.g. chat.message-sent) to RTC's own types and continues the publisher's trace from the traceparent header." "C# / EasyNetQ"
                    handleMessages = component "HandleMessages" "Subscribes once per message type found by handler discovery (in StartAsync, so the host is only ready when queues are bound) and dispatches each message to its handlers in a new DI scope. Knows no concrete message types." "ASP.NET Core IHostedService"
                    messageHandlerRegistration = component "MessageHandlerRegistration" "Scans the assembly on startup for IMessageHandler<T> implementations and registers them in DI (handler discovery)." "C# / Reflection"
                    messageHandlers = component "Message handlers" "One IMessageHandler<T> per message type: PingMessage, MessageSent, ParticipantAdded, MessagesSeen. MessageSentHandler pushes to online recipients and publishes rtc.message-delivered; the others only log ids." "C# classes"
                    chatHub = component "ChatHub" "SignalR hub at /hubs/chat?userId=... Clients only receive (MessageReceived). Updates presence on connect/disconnect." "ASP.NET Core SignalR"
                    presenceTracker = component "PresenceTracker" "Who is connected right now - counts connections per user (multiple devices). In-memory, single instance." "C# singleton"
                    clientNotifier = component "IClientNotifier" "Abstraction over push (SignalRClientNotifier via IHubContext), so handlers can be tested without SignalR." "C# Interface"
                }
                // Fremtidig udvidelse, jf. beslutning: egen DB til fx de seneste 30 notifikationer.
                // realTimeCommunicationDb = container "RealTimeCommunicationDB" "Stores recent notifications (e.g. last 30)." "RDBMS" "Database"
            }

            // RabbitMQ ligger "løst" i systemet (ikke i en group) - den er delt infrastruktur på tværs
            // af bounded contexts, ikke selv en context nogen af teamsene ejer.
            rabbitMq = container "RabbitMQ" "Message broker for event-driven kommunikation mellem microservices - bredere end kun notifikationer." "RabbitMQ" "Message Broker"
        }

        // Relationships

        user -> client "Uses"

        client -> userAuthService "Login"
        userAuthService -> client "Authenticate -> session token"

        client -> userService "manages User"
        client -> channelService "Interacts with"
        client -> chatService "REST/JSON (X-User-Id header)"
        client -> engagementService "Interacts with"

        userService -> userAuthService "Validate"

        userAuthService -> authDb "Read/Write"
        userService -> userDb "Read/Write"
        channelService -> channelDb "Read/Write"
        chatService -> chatDb "Read/Write (Dapper)"
        engagementService -> engagementDb "Read/Write"

        // RabbitMQ bruges bredere end notifikationer - services publisher events til broker'en,
        // og Real-Time Communication Service er én af (potentielt flere) forbrugere, der abonnerer.
        channelService -> rabbitMq "Publish: update"
        engagementService -> rabbitMq "Publish: engagement"
        chatService -> rabbitMq "Publish: chat.* events"
        rabbitMq -> chatService "Deliver: rtc.message-delivered"
        rabbitMq -> realTimeCommunicationService "Deliver: subscribed chat.* events"
        realTimeCommunicationService -> rabbitMq "Publish: rtc.message-delivered"

        client -> realTimeCommunicationService "Connects (SignalR, /hubs/chat)"
        realTimeCommunicationService -> client "Push notification"

        // Component-level relationships for ChatService
        client -> chatsController "Create chats, add participants (REST/JSON)"
        client -> chatMessagesController "Send, read, edit, delete, mark seen (REST/JSON)"
        chatsController -> chatAppService "Calls"
        chatMessagesController -> messageAppService "Calls"
        chatAppService -> domainModel "Applies rules via"
        messageAppService -> domainModel "Applies rules via"
        chatAppService -> repositories "Load/save chats and participants"
        messageAppService -> repositories "Load/save messages and receipts"
        repositories -> chatDb "SQL via Npgsql"
        chatAppService -> chatMessageClient "Publish chat.participant-added"
        messageAppService -> chatMessageClient "Publish chat.message-sent/-edited/-deleted, chat.messages-seen"
        messageDeliveredConsumer -> chatMessageClient "Subscribe rtc.message-delivered"
        messageDeliveredConsumer -> messageAppService "MarkDeliveredAsync"
        chatMessageClient -> chatRabbitMqMessageClient "Implemented by"
        chatRabbitMqMessageClient -> rabbitMq "Publish/Subscribe via EasyNetQ"

        // Component-level relationships for Real-Time Communication Service
        rtcMessagesController -> rtcMessageClient "Publish<PingMessage>"
        handleMessages -> rtcMessageClient "Subscribe<T> per discovered message type"
        handleMessages -> messageHandlers "Dispatches message to"
        messageHandlerRegistration -> messageHandlers "Discovers and registers"
        messageHandlerRegistration -> handleMessages "Provides message types (MessageHandlerRegistry)"
        rtcMessageClient -> rtcRabbitMqMessageClient "Implemented by"
        rtcRabbitMqMessageClient -> rabbitMq "Publish/Subscribe messages via EasyNetQ (IBus)"
        client -> chatHub "Connects, receives MessageReceived"
        chatHub -> presenceTracker "Connected/Disconnected"
        messageHandlers -> presenceTracker "Who of the recipients is online?"
        messageHandlers -> clientNotifier "Push MessageReceived"
        clientNotifier -> chatHub "Clients.Users(...) via IHubContext"
        messageHandlers -> rtcMessageClient "Publish<MessageDelivered>"
    }

    views {
        systemContext "Bizcord" {
            include *
            autoLayout
        }
        container bizcord "Containers" {
            include *
            autoLayout
        }
        component chatService "ChatServiceComponents" {
            include *
            autoLayout
        }
        component realTimeCommunicationService "RealTimeCommunicationComponents" {
            include *
            autoLayout lr 300 150
        }

        styles {
            element "Person" {
                shape Person
                background #2ecc71
                color #ffffff
            }
            element "Client App" {
                shape WebBrowser
            }
            element "Service" {
                shape Hexagon
            }
            element "Database" {
                shape Cylinder
                background #dce6f1
            }
            element "Event-hub" {
                shape Hexagon
                background #d5f5dc
            }
            element "Message Broker" {
                shape Pipe
                background #f5d76e
            }
        }
    }

}
