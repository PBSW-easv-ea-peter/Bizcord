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

            group "Message" {
                messageService = container "Message Service" "Handles personal- and channel-based chat." "ASP .NET Core Web API" "Service"
                messageDb = container "MessageDB" "Contains message information: chat, members, messages." "RDBMS" "Database"
            }

            group "Engagement" {
                engagementService = container "Engagement Service" "Handles likes, reactions etc." "" "Service"
                engagementDb = container "EngagementDB" "Contains engagement information: server_id, message_id, user_id, reaction." "RDBMS" "Database"
            }

            group "RealTimeCommunication" {
                realTimeCommunicationService = container "Real-Time Communication Service" "Subscribes to domain events via RabbitMQ (EasyNetQ) and pushes real-time updates/notifications to clients." "ASP.NET Core Web API (EasyNetQ)" "Event-hub" {
                    messagesController = component "MessagesController" "Manual test endpoint (POST /api/Messages) used to verify the publish pipeline. Not a finalized part of the domain API - more/other endpoints may be added later." "ASP.NET Core Web API Controller"
                    messageClient = component "IMessageClient" "Abstraction over the message broker (Publish/Subscribe), decouples the service from a specific broker technology so it can be swapped later (e.g. Kafka)." "C# Interface"
                    rabbitMqMessageClient = component "RabbitMqMessageClient" "EasyNetQ-based implementation of IMessageClient that talks to RabbitMQ." "C# / EasyNetQ"
                    handleMessages = component "HandleMessages" "Subscribes via IMessageClient on startup; currently only logs received messages." "ASP.NET Core BackgroundService"
                }
                // Fremtidig udvidelse, jf. beslutning: egen DB til fx de seneste 30 notifikationer.
                // realTimeCommunicationDb = container "RealTimeCommunicationDB" "Stores recent notifications (e.g. last 30)." "RDBMS" "Database"

                // TODO: ingen komponent pusher endnu til klienten (fx via SignalR/WebSockets).
                // Kendt gap ift. L2-relationen "Push notification" - afventer beslutning om push-teknologi.
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
        client -> messageService "Communicates with"
        client -> engagementService "Interacts with"

        userService -> userAuthService "Validate"

        userAuthService -> authDb "Read/Write"
        userService -> userDb "Read/Write"
        channelService -> channelDb "Read/Write"
        messageService -> messageDb "Read/Write"
        engagementService -> engagementDb "Read/Write"

        // RabbitMQ bruges bredere end notifikationer - services publisher events til broker'en,
        // og Real-Time Communication Service er én af (potentielt flere) forbrugere, der abonnerer.
        channelService -> rabbitMq "Publish: update"
        messageService -> rabbitMq "Publish: message"
        engagementService -> rabbitMq "Publish: engagement"
        realTimeCommunicationService -> rabbitMq "Subscribe"

        realTimeCommunicationService -> client "Push notification"

        // Component-level relationships for Real-Time Communication Service
        messagesController -> messageClient "Publish<PingMessage>"
        handleMessages -> messageClient "Subscribe<PingMessage>"
        rabbitMqMessageClient -> rabbitMq "Publish/Subscribe messages via EasyNetQ (IBus)"
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