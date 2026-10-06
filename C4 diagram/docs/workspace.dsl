workspace "Bizcord" "C4 model - Level 2 (Container diagram)" {

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
                chatService = container "ChatService" "Handles direct and group chats: participants, messages and read receipts." "ASP.NET Core Web API" "Service"
                chatDb = container "ChatDB" "Contains chats, participants, messages and message receipts." "PostgreSQL" "Database"
            }

            group "Engagement" {
                engagementService = container "Engagement Service" "Handles likes, reactions etc." "" "Service"
                engagementDb = container "EngagementDB" "Contains engagement information: server_id, message_id, user_id, reaction." "RDBMS" "Database"
            }

            group "RealTimeCommunication" {
                realTimeCommunicationService = container "Real-Time Communication Service" "Subscribes to domain events via RabbitMQ (EasyNetQ) and pushes real-time updates/notifications to clients." "ASP.NET Core Web API (EasyNetQ)" "Event-hub" {
                    messagesController = component "MessagesController" "Manual test endpoint (POST /api/Messages) used to verify the publish pipeline. Not a finalized part of the domain API - more/other endpoints may be added later." "ASP.NET Core Web API Controller"
                    messageClient = component "IMessageClient" "Abstraction over the message broker (Publish/Subscribe), decouples the service from a specific broker technology so it can be swapped later (e.g. Kafka)." "C# Interface"
                    rabbitMqMessageClient = component "RabbitMqMessageClient" "EasyNetQ-based implementation of IMessageClient that talks to RabbitMQ. Maps logical event names (e.g. chat.message-sent) to RTC's own types and continues the publisher's trace from the traceparent header." "C# / EasyNetQ"
                    handleMessages = component "HandleMessages" "Subscribes once per message type found by handler discovery (in StartAsync, so the host is only ready when queues are bound) and dispatches each message to its handlers in a new DI scope. Knows no concrete message types." "ASP.NET Core IHostedService"
                    messageHandlerRegistration = component "MessageHandlerRegistration" "Scans the assembly on startup for IMessageHandler<T> implementations and registers them in DI (handler discovery)." "C# / Reflection"
                    messageHandlers = component "Message handlers" "One IMessageHandler<T> per message type: PingMessage, MessageSent, ParticipantAdded, MessagesSeen. MessageSentHandler pushes to online recipients and publishes rtc.message-delivered; the others only log ids." "C# classes"
                    chatHub = component "ChatHub" "SignalR hub at /hubs/chat?userId=... Clients only receive (MessageReceived). Updates presence on connect/disconnect." "ASP.NET Core SignalR"
                    presenceTracker = component "PresenceTracker" "Who is connected right now - counts connections per user (multiple devices). In-memory, single instance." "C# singleton"
                    clientNotifier = component "IClientNotifier" "Abstraction over push (SignalRClientNotifier via IHubContext), so handlers can be tested without SignalR." "C# Interface"
                }
                // Future extension, per decision: own DB for e.g. the latest 30 notifications.
                // realTimeCommunicationDb = container "RealTimeCommunicationDB" "Stores recent notifications (e.g. last 30)." "RDBMS" "Database"
            }

            // RabbitMQ sits "loose" in the system (not in a group) - it is shared infrastructure across
            // bounded contexts, not itself a context owned by any of the teams.
            rabbitMq = container "RabbitMQ" "Message broker for event-driven communication between microservices - broader than just notifications." "RabbitMQ" "Message Broker"
        }

        // Relationships

        user -> client "Uses"

        client -> userAuthService "Login"
        userAuthService -> client "Authenticate -> session token"

        client -> userService "manages User"
        client -> channelService "Interacts with"
        client -> chatService "REST (X-User-Id header)"
        client -> engagementService "Interacts with"

        userService -> userAuthService "Validate"

        userAuthService -> authDb "Read/Write"
        userService -> userDb "Read/Write"
        channelService -> channelDb "Read/Write"
        chatService -> chatDb "Read/Write"
        engagementService -> engagementDb "Read/Write"

        // RabbitMQ is used more broadly than notifications - services publish events to the broker,
        // and Real-Time Communication Service is one of (potentially several) consumers that subscribe.
        channelService -> rabbitMq "Publish: update"
        chatService -> rabbitMq "Publish: chat.message-sent, chat.message-edited, chat.message-deleted, chat.participant-added, chat.messages-seen"
        engagementService -> rabbitMq "Publish: engagement"
        realTimeCommunicationService -> rabbitMq "Subscribe: chat.message-sent, chat.participant-added, chat.messages-seen; Publish: rtc.message-delivered"
        chatService -> rabbitMq "Subscribe: rtc.message-delivered (sets deliveredAt)"

        client -> realTimeCommunicationService "Connects (SignalR, /hubs/chat)"
        realTimeCommunicationService -> client "Push notification"

        // Component-level relationships for Real-Time Communication Service
        messagesController -> messageClient "Publish<PingMessage>"
        handleMessages -> messageClient "Subscribe<T> per discovered message type"
        handleMessages -> messageHandlers "Dispatches message to"
        messageHandlerRegistration -> messageHandlers "Discovers and registers"
        messageHandlerRegistration -> handleMessages "Provides message types (MessageHandlerRegistry)"
        rabbitMqMessageClient -> rabbitMq "Publish/Subscribe messages via EasyNetQ (IBus)"
        client -> chatHub "Connects, receives MessageReceived"
        chatHub -> presenceTracker "Connected/Disconnected"
        messageHandlers -> presenceTracker "Who of the recipients is online?"
        messageHandlers -> clientNotifier "Push MessageReceived"
        clientNotifier -> chatHub "Clients.Users(...) via IHubContext"
        messageHandlers -> messageClient "Publish<MessageDelivered>"
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
        component realTimeCommunicationService "RealTimeCommunicationComponents" {
            include *
            autoLayout
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