# GDA ERP

A .NET 10 microservice ERP: Assets, Property, and a separate Identity service that owns
authentication and role-based authorization for all of them.

---

## Run it

You need **Docker** and the **.NET 10 SDK**.

```bash
git clone <this repo>
cd ERP

docker compose up -d          # Postgres + all three services + the gateway
```

That is the whole setup. On first start each service **creates its own tables** and the Identity
service **seeds the Super Admin account**, so there is nothing to run by hand.

Give it about 20 seconds, then sign in:

```bash
curl -X POST http://localhost:5000/auth-service/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"superadmin","password":"SuperAdmin@123"}'
```

### Super Admin credentials

| | |
|---|---|
| **Username** | `superadmin` |
| **Password** | `SuperAdmin@123` |
| **Email** | `superadmin@erp.local` |

These live in [`src/Services/Identity/Identity.Api/seed.json`](src/Services/Identity/Identity.Api/seed.json)
and are **development only** — the seeder ignores them unless `ASPNETCORE_ENVIRONMENT=Development`
and generates a random password instead, so they can never become a production login.

### Running without Docker

```bash
docker compose up -d postgres         # just the database

dotnet run --project src/Services/Identity/Identity.Api            # :5142
dotnet run --project src/Services/AssetsManagement/AssetsManagement.Api   # :5139
dotnet run --project src/Services/PropertyManagement/PropertyManagement.Api # :5141
dotnet run --project src/ApiGateway/YarpApiGateway                 # :5195
```

---

## Ports

| Service | Through the gateway | Direct (compose) | Direct (`dotnet run`) |
|---|---|---|---|
| Identity | `/auth-service/*` | 5142 | 5142 |
| Assets Management | `/assets-management-service/*` | 5139 | 5139 |
| Property Management | `/property-management-service/*` | 5141 | 5141 |
| Gateway | — | **5000** | 5195 |
| PostgreSQL | — | 5432 | 5432 |

Every service also publishes OpenAPI at `/openapi/v1.json`.

---

## Using the API

Sign in, then send the access token as a bearer token:

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/auth-service/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"superadmin","password":"SuperAdmin@123"}' \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/auth-service/auth/me
```

`/auth/me` returns your roles and effective permissions — that is what a UI uses to decide which
actions to show.

### Creating an employee account

Employees cannot register themselves. A Super Admin provisions them:

```bash
# 1. see what permissions exist
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/auth-service/permissions

# 2. make a role
curl -X POST http://localhost:5000/auth-service/roles \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"role":{"code":"ASSET_VIEWER","name":"Asset Viewer"}}'

# 3. give the role its permissions
curl -X POST http://localhost:5000/auth-service/roles/<roleId>/permissions \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"permissionIds":["<assetsViewPermissionId>"]}'

# 4. register the employee — omit the password and one is generated and returned once
curl -X POST http://localhost:5000/auth-service/users \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"user":{"username":"ahmed","email":"ahmed@erp.local","displayName":"Ahmed Khan"}}'

# 5. assign the role
curl -X POST http://localhost:5000/auth-service/users/<userId>/roles \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"roleId":"<roleId>"}'
```

Ahmed can now sign in and will hold exactly the permissions his role grants.

---

## Layout

```
src/
├── ApiGateway/YarpApiGateway              reverse proxy, per-IP rate limiting
├── BuildingBlocks/
│   ├── BuildingBlock                      CQRS, Result<T>, pagination, validation behaviour
│   ├── BuildingBlock.Authentication       JWT validation, ICurrentUser, PermissionCatalog,
│   │                                      .RequirePermission() — shared by every service
│   └── BuildingBlock.Messaging            placeholder, no message bus yet
└── Services/
    ├── Identity              users, roles, permissions, sessions, login audit
    ├── AssetsManagement      asset taxonomy, dynamic attributes, depreciation, inventory
    ├── PropertyManagement    properties, geo-locations, documents
    └── HRM                   empty — not started
```

Each service follows the same four layers: `Domain`, `Application`, `Infrastructure`, `Api`.

### Database

All services share one PostgreSQL database, **`ERP_DB`**, with a schema each:

| Service | Schema |
|---|---|
| PropertyManagement | `public` |
| AssetsManagement | `assets` |
| Identity | `auth` |

A new service follows the same pattern: `HasDefaultSchema("<name>")` plus
`MigrationsHistoryTable("__EFMigrationsHistory", "<name>")`, so its migrations never touch
another service's tables.

---

## Notes and current limits

* **Business endpoints are not protected yet.** Identity issues tokens and enforces its own
  permissions, but the Assets and Property endpoints are still anonymous — wiring
  `.RequirePermission(...)` into them is the next piece of work. See
  [docs/auth-service-requirements.md](docs/auth-service-requirements.md) §7.3.
* **MFA** is present in the schema but TOTP verification is not implemented.
* **HRM does not exist**, so `employeeId` on a user is an unvalidated reference for now.

### Resetting

```bash
docker compose down -v        # also drops the database volume
docker compose up -d          # recreates and reseeds everything
```

To reset only the Identity data:

```bash
docker compose exec postgres psql -U postgres -d ERP_DB -c 'DROP SCHEMA auth CASCADE;'
```

---

## Before deploying anywhere real

1. Set `Jwt__SigningKey` to a strong secret (32+ bytes) — the committed one is development only.
2. Set `ASPNETCORE_ENVIRONMENT` to something other than `Development`, which makes the seeder
   ignore `seed.json` and generate a random Super Admin password.
3. Change the Postgres password in `compose.yaml`.
4. Set `Database__AutoMigrate=false` and apply migrations as a deliberate deployment step.
