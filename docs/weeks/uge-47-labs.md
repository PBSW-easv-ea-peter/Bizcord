# Implement a Sidecar for logging

The objective of this assignment is to implement a Sidecar for **your** microservice.

## Contents

- [Implement a Sidecar for logging](#implement-a-sidecar-for-logging)
    - [Objective](#objective)
    - [Task: Setup a Sidecar](#task-setup-a-sidecar)
        - [Requirements](#requirements)
        - [Example Setup](#example-setup)
    - [Task: Forward logs to a central system (Optional)](#task-forward-logs-to-a-central-system-optional)
- [Add a GraphQL Presentation Layer](#add-a-graphql-presentation-layer)
    - [Objective](#objective-1)
    - [Task](#task)
        - [Requirements](#requirements-1)
    - [Example: Define a GraphQL Schema](#example-define-a-graphql-schema)

## Objective

Implement a sidecar container to collect logs from your microservice by reading stdout. The sidecar will forward these logs to a central system (simulated by outputting to the console).

You'll use Fluentd as your logging agent and Docker Compose to orchestrate the setup.

## Task: Setup a Sidecar

This task will help to illustrate the benefits of decoupling peripheral concerns from your core business logic. It will allow you to maintain any logging within your app and make the sidecar handle the details of shipping logs to a central system.

### Requirements

1. Configure your microservice to write logs to stdout.
2. Add a Fluentd sidecar container to your `docker-compose.yml` file.
3. Fluentd will read logs from stdout and forward them to a central system (console).

### Example Setup

The following `docker-compose.yml` file shows how to configure your microservice to write logs to stdout and how to add a Fluentd sidecar container.
```yaml
services:
  your-service:
    build: .
    container_name: your-service
    logging:
      driver: "json-file"
    depends_on:
      - fluentd

  fluentd:
    image: fluent/fluentd:latest
    container_name: fluentd
    ports:
      - "24224:24224"
      - "24224:24224/udp"
    volumes:
      - ./fluentd/fluent.conf:/fluentd/etc/fluent.conf
      - /var/lib/docker/containers:/fluentd/log/containers:ro
    environment:
      - FLUENTD_CONF=fluent.conf
```

This approach requires a bit of configuration to ensure that the sidecar can read the logs from stdout. Through the `fluent.conf` file, you can configure Fluentd to read the logs directly from the Docker log file path and print them to its own stdout, simulating the behavior of a sidecar.
```
<source>
  @type tail
  path /fluentd/log/containers/*/*.log
  pos_file /fluentd/log/fluentd-docker.pos
  tag docker.*
  format json
</source>

<match **>
  @type stdout
</match>
```

## Task: Forward logs to a central system (Optional)

In this task, you'll extend your sidecar to forward logs to a central system. You can follow this article for details about the setup.

>Linket til artiklen fulgte ikke med i PDF'en – find det på Moodle-siden (Task 01).

# Add a GraphQL Presentation Layer

Your microservice currently exposes only a REST API. You will now add a GraphQL presentation layer (BFF-style) that exposes a tailored query interface for frontend clients.

## Objective

This new service will aggregate and shape data as needed for the UI — possibly combining multiple REST endpoints into a single GraphQL query.

## Task

This task will help enforce the benefits of decoupling **client** concerns from your core business logic. It will improve maintainability by having a clean separation between UI needs and your microservice's core functionality.

### Requirements

1. Add a new GraphQL presentation layer to your microservice.
2. Use HotChocolate to implement the GraphQL API.
3. Reuse your existing service layer to implement the new presentation layer.

## Example: Define a GraphQL Schema

Using HotChocolate, you can define a GraphQL schema in different ways. The following example shows how to define a schema using implementation-first approach.
```c#
[QueryType]
public static class ChannelQuery
{
    public static async Task<IEnumerable<Channel>> GetUserChannels(Guid userId, [Service ...
    {
        return await channelService.GetUserChannelsAsync(userId);
    }
}
```

And for a mutation, you can define a mutation like this:
```c#
public class ChannelMutation
{
    public static async Task<Channel> CreateChannel(ChannelInput input, [Servic ...
    {
        return await channelService.CreateChannelAsync(input);
    }
}
```

>Signaturerne er afskåret i PDF'en efter `[Servic`. Ud fra registreringen nedenfor er parameteren formentlig en keyed service, fx `[Service("channelService")] ChannelService channelService)` – tjek originalen på Moodle.

Finally, you can register the GraphQL server, queries, mutations and dependencies like this:
```c#
builder.Services
    .AddGraphQLServer()
    .AddQueryType<ChannelQuery>()
    .AddMutationType<ChannelMutation>()
    .AddKeyedScopedService<ChannelService>("channelService");
```
