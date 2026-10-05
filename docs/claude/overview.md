# GDA ERP: platform overview for AI assistants

Read this first, then the file for the service you are working on:

| Working on | Read |
| --- | --- |
| `src/Services/Identity` | [identity.md](identity.md) |
| `src/Services/PropertyManagement` | [property-management.md](property-management.md) |
| `src/Services/AssetsManagement` | [assets-management.md](assets-management.md) |
| `src/ApiGateway` | [api-gateway.md](api-gateway.md) |
| `src/BuildingBlocks` | this file, "Building blocks" |

## What this is

The ERP for GDA (Galiyat Development Authority), as .NET microservices. Each service owns one schema in a single
PostgreSQL database:

| Service | Port (dotnet run) | Compose | Gateway prefix | DB schema |
| --- | --- | --- | --- | --- |
| Identity (auth, users, roles, permissions) | 5142 | `identity-api` 5142:8080 | `/auth-service/` | `auth` |
| Property Management | 5141 | `poperty-management-api` 5141:8080 (sic) | `/property-management-service/` | `property` |
| Assets Management | 5139 | `assets-management-api` 5139:8080 | `/assets-management-service/` | `assets` |
| YARP API gateway | 5195 | `yarpapigateway` 5000:8080 | n/a | n/a |
| PostgreSQL 17 | 5432 | `postgres` | n/a | db `ERP_DB` |

`src/Services/HRM` is an empty placeholder and isn't in the solution yet.

## Running it

- **All in Docker**: run `docker compose up -d` from the repo root.
- **Database in Docker, services from the IDE**: run `docker compose up -d postgres`, then `dotnet run` each Api
  project. All `appsettings.json` files point at
  `Host=localhost;Port=5432;Database=ERP_DB;Username=postgres;Password=YourStrongPassword123!`.
- Services migrate themselves on startup (`Database:AutoMigrate`, default on in Development), so you never need
  `dotnet ef database update` by hand. Identity and Property also seed (the admin user, permissions, master data);
  Assets seeds nothing, so its lookups start empty.
- Log in with `POST http://localhost:5142/auth/login` and `{"username":"superadmin","password":"SuperAdmin@123"}`.
  Send `Authorization: Bearer <accessToken>` to the other services.
- Each service serves OpenAPI at `/openapi/v1.json`; there is no Swagger UI. Property has a step-by-step
  `PropertyManagement.Api.http`.
- `dotnet build src/ERP.slnx` and `dotnet test src/ERP.slnx`. The tests are Property domain tests in `src/Tests`.

## Architecture (the same in every service)

Clean Architecture with four projects per service. Project references go inward only:

```text
X.Domain          entities, aggregates, value objects, enums, domain events, DomainException. No EF.
X.Application     Features/<Area>/Commands|Queries/<UseCase>/ (MediatR + FluentValidation), DTOs,
                  IApplicationDbContext (the DbContext as an interface; handlers query it with LINQ)
X.Infrastructure  ApplicationDbContext, EF configurations, migrations, seeders, interceptors, file storage
X.Api             Carter endpoint modules, exception handler, Program.cs, DependencyInjection.cs
```

- **CQRS**: `ICommand<T>` / `IQuery<T>` with `ICommandHandler` / `IQueryHandler` (BuildingBlock/CQRS). Handlers
  return `Result<T>`. A failed result becomes `400 {message}`. Business rules live in domain methods and throw
  `DomainException`, which the API returns as 400. Keep handlers thin.
- **Validation**: a FluentValidation validator per command, run by `ValidationBehavior`. A failure is a 400
  ProblemDetails with `ValidationErrors: [{propertyName, errorMessage}]`.
- **Endpoints**: Carter `ICarterModule` calling `ISender.Send`.
- **Errors**: each service has its own `IExceptionHandler` (`PropertyExceptionHandler`, `IdentityExceptionHandler`,
  `AssetExceptionHandler`) producing ProblemDetails with these statuses:
  - 400: validation or domain error
  - 404: `NotFoundException`
  - 409: DB update or concurrency conflict
  - 500: anything else
- **Ids**: Guid strongly typed ids (`sealed record PropertyId { Guid Value; static Of(Guid) }`). `Of(Guid.Empty)` throws.
- **EF Core 10 + Npgsql**:
  - Each service calls `HasDefaultSchema(Schema)`, keeps its own `__EFMigrationsHistory` in that schema, and uses
    `UseSnakeCaseNamingConvention()`.
  - Migrations live in `X.Infrastructure/Data/Migrations`.
  - New migration (from `src/`): `dotnet ef migrations add <Name> -p Services/X/X.Infrastructure -s Services/X/X.Api`.
- **Audit**: `AuditableEntityInterceptors` stamps created and updated columns. It differs per service; see the
  service files.
- **No namespaces.** Every type is in the global namespace, so class names like `DependencyInjection`,
  `ApplicationDbContext`, `DomainException` and `Entity<T>` repeat across services. Follow this and don't add
  `namespace` lines. Indentation is 2 spaces.
- Each csproj pins its own package versions; there is no central package management. Versions in use:
  - EF Core 10.0.10
  - Npgsql.EFCore 10.0.3
  - EFCore.NamingConventions 10.0.1
  - Carter 10.0.0
  - MediatR 14.2.0
  - FluentValidation 12.1.1
  - Mapster 10.0.11

## Building blocks (`src/BuildingBlocks`)

- **BuildingBlock**:
  - CQRS interfaces.
  - `Result<T>`: public **fields** `IsSuccess`, `Value`, `Message`; created with `Result<T>.Success(v)` or
    `.Failure(msg)`.
  - `PaginationRequest(Pageindex, PageSize)`: note the lower-case `i`. `PaginatedResult<T>(pageIndex, pageSize, count, data)`.
  - `NotFoundException`.
  - `ValidationBehavior` and `LoggingBehaviors`.
  - Validation helpers `.Code()` and `.Name()`.
- **BuildingBlock.Authentication**:
  - `AddErpAuthentication(config)`: JWT bearer with `MapInboundClaims = false`, which is required. It throws at
    startup if `Jwt:SigningKey` is empty.
  - `.RequirePermission(PermissionCatalog.X.Y)` on routes or groups.
  - `ICurrentUser`: `UserId`, `Username`, `Roles`, `Permissions`, `IsAuthorizedOfficer`, `HasPermission()`, `AuditName`.
  - `ErpClaimTypes`.
  - **`PermissionCatalog`**: every permission code in the ERP. The Identity seeder reads it.
- **BuildingBlock.Messaging**: an empty placeholder; there is no message bus yet.

## Security model in one paragraph

Identity issues short-lived HS256 JWTs (15 min by default, editable in the security settings) carrying `perm` claims (effective permissions) and `role`, `sub`, `username`
and `ao` (authorized officer). Every service validates them with the **same** `Jwt:SigningKey`, issuer `erp-identity`
and audience `erp`. The dev key is in each `appsettings.Development.json`, and compose passes `Jwt__SigningKey`.
Endpoints declare `.RequirePermission(...)`. **Identity and Property enforce this, but Assets does not yet: its
endpoints are anonymous.** Adding auth to Assets means referencing BuildingBlock.Authentication in its csproj, adding
`AddErpAuthentication`, `UseAuthentication/UseAuthorization` and `RequirePermission` on routes, copying all of
`BuildingBlocks/` in its Dockerfile, and setting `Jwt__SigningKey` in compose.

## Team workflow

- `main` is the shared branch. Contributors push a feature branch (e.g. `assets-redesign`,
  `property-redesign-(saad)`); the repo owner reviews it and merges it into `main`. Don't push to `main` unless asked.
- Base your branch on the latest `main`. If you change a service's EF model, **regenerate your migration after
  rebasing** if `main` gained migrations for that service. Two branches that each add a migration from the same
  snapshot don't fit together even when git merges them cleanly.
- `.claude/` is gitignored.

## Known gotchas (platform)

- `README.md` is out of date in places: it says the Property schema is `public` (it's `property`) and that Property
  endpoints are anonymous (they require permissions). Trust the code and these files.
- The compose service is spelled `poperty-management-api`, and the gateway override uses that name. Rename both
  together or neither.
- `src/.dockerignore` excludes `appsettings.Development.json`, so containers get the JWT key only from compose env vars.
- `LoggingBehaviors` is constrained to the non-generic `IRequest`, so it never runs for the `ICommand<T>` /
  `IQuery<T>` requests.
- `BuildingBlock/Exceptions/Handler/CustomeExceptionHandler.cs` is unused and buggy; services use their own handlers.
- Property's https launch profile uses port 5142, which clashes with Identity's http port. Use the http profiles.
- Assets runs `MigrateAsync()` with no wait-for-database retry, and compose has no healthchecks. If Postgres isn't up
  yet, start Assets again.
- No CORS and no health checks are configured anywhere yet.
