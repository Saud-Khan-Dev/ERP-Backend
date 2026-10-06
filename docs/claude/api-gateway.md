# API gateway (YARP) — context for AI assistants

Read [overview.md](overview.md) first. This file covers `src/ApiGateway/YarpApiGateway`.

## What it is

A thin YARP reverse proxy that puts all services behind one address. Its only package is `Yarp.ReverseProxy` 2.3.0.
It does **no** authentication, CORS, OpenAPI aggregation or forwarded-headers handling. Each service validates
JWTs itself.

- Port: `http://localhost:5195` (https 7116) with `dotnet run`, and host **5000** in compose (`yarpapigateway`).
- `Program.cs` calls `AddReverseProxy().LoadFromConfig("ReverseProxy")`, then `UseRateLimiter()`, then
  `MapReverseProxy()`.

## Routes (`appsettings.json`, section `ReverseProxy`)

| Incoming path | Cluster | Destination (local) |
| --- | --- | --- |
| `/auth-service/{**catch-all}` | `identity-cluster` | `http://localhost:5142` |
| `/property-management-service/{**catch-all}` | `property-management-cluster` | `http://localhost:5141` |
| `/assets-management-service/{**catch-all}` | `assets-management-cluster` | `http://localhost:5139` |
| `/hrm-service/{**catch-all}` | `hrm-cluster` | `http://localhost:5144` |

Each route has the transform `PathPattern: "{**catch-all}"`, which **strips the prefix**: for example,
`/auth-service/auth/login` arrives at Identity as `/auth/login`.

In compose, destinations are overridden with environment variables shaped like
`ReverseProxy__Clusters__<cluster>__Destinations__<destination>__Address=http://<container>:8080`. The property one
points at the container name `poperty-management-api` (the typo is real).

## Rate limiting

There is one fixed-window policy, `"fixed"`, applied to every route:
- 100 requests per 10 seconds per client IP (`RateLimiting:PermitLimitPer10Seconds`), answering 429 when exceeded.
- It's partitioned by `Connection.RemoteIpAddress`. There is no stricter limit for login.

## Adding a service

1. Add a route with a new prefix (`/<name>-service/{**catch-all}`), `RateLimiterPolicy: "fixed"` and the same
   `PathPattern` transform.
2. Add a cluster with one destination at the service's local port.
3. Add the compose environment override for the container address, and `depends_on`.

## Gotchas

- Behind the gateway, services see the **gateway's IP** as the caller's IP. No forwarded headers are configured, and
  Identity's login audit reads `RemoteIpAddress` on purpose.
- OpenAPI documents aren't proxied under one URL. Read each service's `/openapi/v1.json` directly, or through its
  prefix (`/auth-service/openapi/v1.json`).
