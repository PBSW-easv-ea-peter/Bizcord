# Uge 43 — Labs (API Gateway)

## Task 01: Add a routing configuration for your microservice

**Krav for gennemførelse** — Færdig: Se

### Add a routing configuration for your microservice to the API gateway

The objective of this task is to add a routing configuration for your microservice to the API gateway configuration file.

### Ocelot

Ocelot is a .NET library that provides a way to configure and manage an API gateway. It is a popular choice for building API gateways in .NET applications. In Ocelot, routing is configured in JSON. In this starter, edit the source files in `src/Configuration/ocelot.*.json`; Ocelot merges them into a generated `ocelot.json` when the gateway starts.

Please refer to the [Ocelot documentation](https://ocelot.readthedocs.io/) for more information on how to configure the routing.

### Task

In your microservice solution, you're implementing a REST API for your domain. And so are the other teams. This means that any client application will have to know about all the **different** hosts of the microservices within the system.

To have a **single entry point** for the client application, you're going to map your microservice's routes to the API gateway routes.

Your microservice stays in its existing repository. Clone the standalone [API gateway repository](https://github.com/patrickstolc/api-gateway) alongside it and run both locally. Each team works in its own gateway clone.

```bash
git clone https://github.com/patrickstolc/api-gateway.git
cd api-gateway
docker compose up --build
```

The gateway runs at `http://localhost:8080`; its Swagger UI is at `http://localhost:8080/swagger`. Start your microservice separately using your existing setup.

1. Edit or replace `src/Configuration/ocelot.example.json`, or add `src/Configuration/ocelot.<your-service>.json`. Add routes for real endpoints in your microservice; the starter's example service is not included.
2. Set the downstream HTTP host and port to your running microservice. With the gateway in Docker, use `host.docker.internal` and your microservice's host/published HTTP port. Inside the gateway container, `localhost` means the gateway itself. The Compose file includes the Linux host mapping; ensure your service is reachable from Docker.
3. Stop and rerun `docker compose up --build` after configuration changes. Test the same endpoint directly and through the gateway, checking the method, path and response.

The repository README includes an optional .NET 10 CLI setup. When both processes run directly on your computer, use `localhost` as the downstream host instead.

### Example

Suppose the `Channel` microservice exposes endpoints to `PUT` and `DELETE` a `Channel`.

```json
{
  "UpstreamHttpMethod": ["Put", "Delete"],
  "UpstreamPathTemplate": "/channel/{channelId}",
  "DownstreamPathTemplate": "/api/channel/{channelId}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [{ "Host": "host.docker.internal", "Port": 5001 }]
}
```

The JSON above will route requests sent to the API gateway at `/channel/{channelId}` to the `Channel` microservice at `/api/channel/{channelId}`. In addition, the `DownstreamHostAndPorts` section specifies that the `Channel` microservice is reachable from the gateway container via `host.docker.internal` at its published HTTP port `5001`. Replace this example port with your own.

> **Commit your changes**
> Commit the routing configuration in your local gateway clone. You do not need to push changes back to the teaching repository. Keep this clone for Task 02.

---

## Task 02: Implement authorization

**Krav for gennemførelse** — Færdig: Se

### Implement authorization for your microservice endpoints

The goal of this task is to add authentication and authorization for your microservice endpoints.

Continue in the same local clone of the [standalone API gateway](https://github.com/patrickstolc/api-gateway) that you configured in Task 01. Your microservice stays in its own repository and runs separately.

### Scope & Requirements

Authentication is implemented on the API gateway level, and the API gateway will forward the authenticated requests to the microservice. For this local exercise, you can implement authentication in the gateway and forward identity claims to your microservice.

Requests that reach your microservice through a protected gateway route have been authenticated. Direct calls to the microservice bypass this check; a real deployment must prevent that bypass before trusting forwarded identity headers. But often you need to know more about the user, e.g. what is their `Role` or what is their `UserId`, meaning that you need to configure authorization for your microservice endpoints.

### Task

Start the gateway at `http://localhost:8080`. Use its local token faucet to create a test JWT with claims you choose:

```bash
curl -sS -X POST http://localhost:8080/faucet \
  -H 'Content-Type: application/json' \
  -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.read"}}'
```

Copy the response's `value` into the `Authorization: Bearer <token>` header when calling the gateway. You can also request a token in Swagger. The faucet and demo signing key are local teaching tools; the faucet does not verify a real user's identity.

#### Authentication

In the locally cloned `api-gateway` solution, you have to add authentication for the routes of your microservice that are protected. This would be any route that requires an authenticated user. Edit your route files in `src/Configuration/ocelot.*.json`; do not edit the generated `ocelot.json`.

#### Authorization

In the locally cloned `api-gateway` solution, you have to add authorization for the routes of your microservice that are protected. Feel free to use either roles or any custom scope you want. Use `RouteClaimsRequirement` for a role claim, or `AllowedScopes` for a `scope` claim, following the [Ocelot authorization documentation](https://ocelot.readthedocs.io/en/latest/features/authorization.html).

In addition, forward and transform claims from the incoming request, e.g. the `UserId` or `Role`, to your microservice. This allows the microservice to receive the authenticated identity without the client supplying a trusted user ID. The starter removes incoming `X-UserId` and `X-Role` headers before claim transformation. If you use other trusted identity-header names, remove client-supplied values before populating them from validated claims.

### Example: Authentication → Protecting endpoints

In your route configuration inside `src/Configuration/ocelot.*.json`, you can protect routes by adding the `AuthenticationOptions` section:

```json
...
"AuthenticationOptions": {
    "AuthenticationProviderKeys": ["Bearer"],
    "AllowedScopes": []
},
...
```

The scheme in `AuthenticationProviderKeys` must match the one configured in the API gateway middleware. In this case, it is `Bearer`, indicating that the API gateway expects a Bearer token in the request header.

The `AllowedScopes` is an array of scopes that are allowed to access the route. In this case, it is empty, which means that any authenticated user can access the route.

### Example: Authorization → Transforming claims

Suppose a microservice, `Channel`, exposes an API endpoint to get all channels for a specific user, e.g. `/api/channel/user/{subjectId}`. We only want to expose this endpoint to the user themselves.

On the client side, we could pass the user's `UserId` in the request, but this would require the client to know the microservice's API and to include the `UserId` in every request.

Instead, we can **transform** the claims in the incoming request to include the `UserId` in the request headers, since the claim is already stored in the JWT token.

#### Using Headers

```json
...
"AddHeadersToRequest": {
    "X-UserId": "Claims[sub] > value"
}
...
```

For the header approach, expose an endpoint such as `/api/channel/user/me` and read the user ID from the forwarded header:

```csharp
[HttpGet("user/me")]
public async Task<IActionResult> GetChannelsForUser(
    [FromHeader(Name = "X-UserId")] string? userId)
{
    // Use userId to load this user's channels.
    ...
}
```

#### Using Path Transformation

Alternatively, keep a downstream endpoint with a user-ID path parameter. Let the gateway fill that parameter from `sub` rather than accepting the identity from the client:

```json
...
"UpstreamPathTemplate": "/channel/me",
"DownstreamPathTemplate": "/api/channel/user/{subjectId}",
"ChangeDownstreamPathTemplate": {
    "subjectId": "Claims[sub] > value"
}
...
```

Choose the header or path approach to suit your microservice. Keep authentication enabled on any route that transforms identity claims.

Rebuild and restart the gateway after changes. Test a protected route without a token, with an invalid token, and with claims that satisfy or fail your role/scope requirement. Verify the forwarded user ID in your microservice, then commit the changes locally.
