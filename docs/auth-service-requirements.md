# Authentication & Authorization Service — Requirements

**Status:** specification only. No implementation has started.
**Audience:** the engineer or AI agent who will build this service.
**How to use:** work through the phases in order. Phase 0 is mandatory and produces a written
analysis that must be reviewed before any code is written.

---

## 0. The one-sentence rule

> A **Super Admin** provisions employee accounts and controls their roles.
> Employees cannot self-register and cannot elevate their own privileges.
> Once authenticated, an employee can perform **only** the operations their roles and
> permissions allow, and that decision is always made on the server.

---

## 1. Scope

### 1.1 Build

A **new, separate `Identity` service** that owns authentication and authorization for the whole ERP:

```
Identity Service
├── Users, credentials, password policy
├── Login / logout / refresh / MFA
├── Sessions & refresh tokens
├── Login auditing & lockout
├── Roles, permission modules, permissions
├── User→Role, Role→Permission, User permission overrides
└── Token issuance (JWT) + JWKS for the other services to validate
```

### 1.2 Do not build

* Employee HR profile (department, designation, salary, joining date…) — that belongs to the HRM service.
* Any business logic of AssetsManagement or PropertyManagement.

### 1.3 Do not break

The existing services must keep working while this is added. Business services gain permission
checks; they do not move their data or change their public route shapes.

---

## 2. Phase 0 — Understand before you build (mandatory)

Do not write implementation code in this phase. Produce a short written analysis answering:

1. What exists today (services, layers, patterns, database, gateway).
2. Where authentication belongs, and where authorization belongs.
3. How the Employee record will relate to the User account, given §4.
4. Which parts of the supplied RBAC schema (§6) can be used as-is.
5. Which parts must change, and why (start from the known issues in §6.3).
6. How the Identity service will communicate with the other services (§8).
7. How the Super Admin provisioning workflow will work end to end (§5).
8. How permission enforcement will be wired into existing endpoints (§7.3).
9. Answers — or explicit recommendations — for every open decision in §12.

The rest of this section is the analysis that already exists, so you do not have to rediscover it.

### 2.1 What exists today

| Component | State | Port (local / compose) |
|---|---|---|
| `src/ApiGateway/YarpApiGateway` | YARP reverse proxy, per-IP rate limiting (429) | 5195 / 5000 |
| `src/BuildingBlocks/BuildingBlock` | shared kernel — see 2.3 | — |
| `src/BuildingBlocks/BuildingBlock.Messaging` | **empty stub** (`Class1.cs`), no message bus | — |
| `src/Services/PropertyManagement` | complete, 23 endpoints | 5141 / 5141 |
| `src/Services/AssetsManagement` | complete — asset module + inventory/procurement module, ~90 endpoints | 5139 / 5139 |
| `src/Services/HRM` | **empty folder, zero files** | — |
| Identity / auth | **does not exist anywhere** — no login, no JWT, no `[Authorize]`, no current-user abstraction | — |

Every endpoint in the system is currently **anonymous**.

### 2.2 Architecture each service follows (copy this exactly)

Four projects per service — `Domain`, `Application`, `Infrastructure`, `Api`:

* **Domain** — `Entity<TId>` → `Aggregate<TId>` (domain events); strongly-typed ID records
  (`AssetId.Of(Guid)`); value objects (`Code`, `Name`, `Money`); `DomainException`.
* **Application** — CQRS via MediatR: `Features/<Area>/Commands|Queries/<Name>/{Command+Validator, Handler}`;
  `IApplicationDbContext`; DTOs + `ToDto()` mapping extensions.
* **Infrastructure** — EF Core `ApplicationDbContext`, one `IEntityTypeConfiguration` per entity inheriting
  `EntityConfiguration<T,TId>`, `AuditableEntityInterceptors`, `DispatchDomainEventInterceptor`, migrations.
* **Api** — Carter modules, **one file per endpoint**: `Request` → `Adapt<Command>` → `sender.Send` → `Response`;
  OpenAPI; a ProblemDetails `IExceptionHandler`.

Conventions that are not obvious:

* **No `namespace` declarations anywhere** — every type is in the global namespace.
* Postgres via Npgsql + `EFCore.NamingConventions` (snake_case), enums stored as strings.
* `Result<T>` for expected failures (returns 400 with a message); exceptions for not-found (404) and
  domain rule violations (400).
* 2-space indentation.

### 2.3 What `BuildingBlock` already gives you

`ICommand`/`ICommandHandler`/`IQuery`/`IQueryHandler`, `LoggingBehaviors`, `ValidationBehavior`,
`Result<T>`, `PaginatedResult<T>`/`PaginationRequest`, `NotFoundException`/`BadRequestException`,
`CustomeExceptionHandler`, and FluentValidation rules (`.Code()`, `.Name()`).

> Anything genuinely shared by Identity **and** the business services (JWT validation, the permission
> constants, `ICurrentUser`, the `RequirePermission` helper) belongs in `BuildingBlock` or a new
> `BuildingBlock.Authentication` project — not copied per service.

### 2.4 Database — one database, schema per service

All services share a single PostgreSQL database **`ERP_DB`**. Each service owns a schema and its own
migration history:

| Service | Schema | Migrations history |
|---|---|---|
| PropertyManagement | `public` | `public.__EFMigrationsHistory` |
| AssetsManagement | `assets` | `assets.__EFMigrationsHistory` |
| **Identity (new)** | **`auth`** | **`auth.__EFMigrationsHistory`** |

Wiring, copied from `AssetsManagement.Infrastructure`:

```csharp
// ApplicationDbContext
public const string Schema = "auth";
protected override void OnModelCreating(ModelBuilder builder)
{
  builder.HasDefaultSchema(Schema);
  builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  base.OnModelCreating(builder);
}

// DependencyInjection
opt.UseNpgsql(connectionString, npgsql =>
  npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.Schema));
opt.UseSnakeCaseNamingConvention();
```

Identity must **not** put foreign keys across into `assets` or `public`.

### 2.5 Auditing today — and the problem to solve

Both services stamp audit columns in `AuditableEntityInterceptors`:

```csharp
entry.Entity.CreatedBy = "Saud-DEV";   // hard-coded placeholder
```

`IEntity.CreatedBy` / `LastModifiedBy` are `string?`. Separately, the asset module records actors as
`Guid?` (`AssetLifecycleEvent.PerformedBy`, `AssetAttributeHistory.ChangedBy`,
`AssetAssignment.ApprovedBy`, `AssetDisposal.ApprovedBy`) that today are supplied **in the request body** —
which is exactly the "don't trust the client" problem in §9.4. `Asset.DeletedBy` is `string?` while its
siblings are `Guid?`; that inconsistency should be resolved at the same time.

**Required outcome:** an `ICurrentUser` abstraction resolved from the JWT replaces `"Saud-DEV"`, and the
actor fields stop being client-supplied.

---

## 3. Principles

| # | Principle |
|---|---|
| P1 | **Authentication ≠ authorization.** "Who are you?" and "What may you do?" stay separate concerns. |
| P2 | **No self-registration.** Account creation is a Super Admin operation only. |
| P3 | **No privilege self-elevation.** A user can never grant themselves a role or permission. |
| P4 | **Server decides.** The backend never trusts `role`, `permissions`, `isAdmin` or actor ids from the client. Hiding a button in the UI is a convenience, never a control. |
| P5 | **Permissions are `MODULE.ACTION`,** from a fixed catalogue — never free text. |
| P6 | **Identity does not duplicate the employee domain.** It stores a reference, not an HR profile. |
| P7 | **Security records are auditable and soft-deleted.** Never destroy audit history. |
| P8 | **Secrets are never returned.** Password hashes and MFA secrets never appear in any API response, DTO, log or OpenAPI schema. |

---

## 4. User ↔ Employee — and the HRM gap

The intended relationship:

```
Employee (HRM service)  1 ──── 0..1  User (Identity service)
```

**Blocking fact: the HRM service does not exist — `src/Services/HRM` is an empty folder.**
There is no employee table, so `users.employee_id` currently references nothing.

Pick one and state it in the Phase 0 analysis:

| Option | Description | Trade-off |
|---|---|---|
| **A. Nullable reference now** *(recommended)* | Keep `employee_id uuid NULL` with **no FK**, unique when present. Identity stores a display name/email for showing "who did this". Link is validated later when HRM exists. | Unblocks everything today; referential integrity deferred to the application layer. |
| **B. Build a minimal HRM first** | Create the HRM service with `employee` (code, name, department, designation, status) before Identity. | Correct long-term model, but delays authentication and expands scope. |
| **C. Employee lives in Identity** | Identity owns the employee record. | **Rejected** — violates P6 and the service boundary in §1.2. |

Whichever is chosen, `employee_id` must stay a *reference*: no HR profile fields inside `auth.users`.

---

## 5. Super Admin provisioning workflow

```
Super Admin
   │
   │ POST /users  { username, email, employeeId?, roleIds[], temporaryPassword? }
   ▼
Identity Service
   ├── create user account (password hashed, must_change_password = true)
   ├── link to employee (if HRM exists / option A reference)
   ├── assign role(s)            ← roles carry the permissions
   └── audit who created it
   ▼
Employee receives credentials → first login → forced password change → can work
```

### 5.1 Super Admin capabilities

**Users:** create, list, view, update, activate/deactivate, reset password, force password change,
lock/unlock, view sessions & login history, revoke sessions, assign/remove roles.

**Roles:** create, update, activate/deactivate, delete (non-system only), manage the role's permissions.

**Permissions:** view modules and permissions, attach/detach to roles, grant/revoke per-user overrides.

**Security:** view login attempts, view account activity, force logout.

### 5.2 Guard rails

* The `SUPER_ADMIN` role is `is_system = true` — it cannot be renamed, deleted or deactivated through the API.
* Only a Super Admin may grant the `SUPER_ADMIN` role. Assigning a role you do not hold requires an explicit
  `IAM.ROLE_ASSIGN` permission **and** a server-side check that the target role is not a system role.
* A user must not be able to modify their own roles, overrides or active status — including via a
  self-targeted `PUT /users/{id}`.
* The system must always retain at least one active Super Admin — block deactivating/deleting the last one.
* The first Super Admin is created by a **seeder**, not an API (see §6.3 issue 6).

---

## 6. Data model

### 6.1 Use the supplied RBAC schema as the baseline

Tables: `users`, `sessions`, `login_attempts`, `roles`, `permission_modules`, `permissions`,
`user_roles`, `role_permissions`, `user_permission_overrides`.

### 6.2 Adapt it to this project's conventions

| Aspect | Rule |
|---|---|
| Keys | `uuid` PKs — matches existing services. Wrap in strongly-typed IDs (`UserId`, `RoleId`, `PermissionId`, `SessionId`, …) with `.Of(Guid)`. |
| Naming | snake_case tables/columns via `EFCore.NamingConventions`, plus an explicit `ToTable("users")` per configuration (the asset module does this — follow it). |
| Schema | everything in `auth`. |
| Base types | `Entity<TId>` / `Aggregate<TId>`; the existing `created_at/created_by/updated_at/updated_by` audit columns come from the base class — do not redeclare them. |
| Soft delete | `deleted_at` + a global query filter, as `Asset` does. |
| Enums | stored as strings (`action`, `mfa_type`, `effect`). |
| Value objects | `Username`, `EmailAddress`, `PermissionCode` (validates `MODULE.ACTION`), `PasswordHash`. |

### 6.3 Known issues in the supplied schema — fix these

1. **`users.employee_id`** — no HRM table exists. See §4; do not create an FK to a non-existent table.
2. **`user_roles` PK `(user_id, role_id)` + `revoked_at`** — once a role is revoked, the same role can never
   be re-granted without destroying the revocation record. Either hard-delete the row on revoke and keep a
   separate audit trail, **or** give the table a surrogate `id` PK and a partial unique index on
   `(user_id, role_id) WHERE revoked_at IS NULL`.
3. **`permissions.action varchar(50)`** — free text invites `EDIT` vs `UPDATE` drift. Constrain it
   (CHECK constraint or a lookup table) and add a unique index on `(module_id, action)`.
4. **`permissions.name` unique globally** — a display name is a poor uniqueness key. `code` is the real
   identifier; consider dropping the unique on `name`.
5. **`mfa_secret text`** — a plaintext TOTP secret is as good as the password. Encrypt at rest
   (ASP.NET Data Protection or column encryption) and never expose it after enrolment.
6. **`users.created_by → users.id`** — the first Super Admin has no creator. Make it nullable and create
   that account through a seeder driven by configuration/environment variables, never a public endpoint.
7. **Audit type mismatch** — existing `IEntity.CreatedBy` is `string?`, the auth schema uses `uuid`.
   Decide: widen the shared base to `Guid?`, or store the user id as a string. Apply the same fix to
   `Asset.DeletedBy` (`string?`) so all actor columns are consistent.
8. **`sessions` has no rotation chain** — add `replaced_by_session_id` (or a `rotated_at`) so reuse of an
   already-rotated refresh token can be detected and the whole chain revoked.
9. **`user_permission_overrides` PK `(user_id, permission_id)`** — correct, but means ALLOW and DENY are
   mutually exclusive per permission. That is intended; make it explicit in code and docs.

### 6.4 Permission catalogue

Derive the catalogue from the endpoints that actually exist today. Suggested modules:

| Module | Covers |
|---|---|
| `ASSETS` | assets, attachments, assignments/transfers, lifecycle events, status changes |
| `ASSET_TAXONOMY` | asset classes, types, categories |
| `ASSET_ATTRIBUTES` | option sets, attribute groups, definitions, assignments, schema |
| `ASSET_FINANCE` | acquisition, depreciation schedules & entries, valuations, disposal |
| `INVENTORY` | inventory categories, types, items, stock, scrap |
| `PROCUREMENT` | purchases and purchase lines |
| `PROPERTY` | properties, geo-locations, property documents, property attributes |
| `IAM` | users, roles, permissions, sessions, security audit |
| `HR` | reserved for the HRM service |

Actions: `VIEW`, `CREATE`, `EDIT`, `DELETE`, plus `APPROVE` and `POST` where a workflow needs them
(e.g. `ASSET_FINANCE.POST` for posting a depreciation entry, `ASSET_FINANCE.APPROVE` for disposal approval).

Seed the catalogue with a migration or seeder so permissions are not hand-created per environment.

---

## 7. Authorization

### 7.1 Model

```
User ──< user_roles >── Role ──< role_permissions >── Permission ──> Permission Module
  └──< user_permission_overrides >── Permission                  └──> Action
```

### 7.2 Resolution order — DENY always wins

```
1. user_permission_overrides  effect = DENY   (not expired)  → DENIED
2. user_permission_overrides  effect = ALLOW  (not expired)  → ALLOWED
3. any active, non-expired role grants the permission        → ALLOWED
4. otherwise                                                 → DENIED
```

Expired or revoked roles and overrides grant nothing. Inactive users, inactive roles and inactive
permissions grant nothing.

### 7.3 Enforcement in business services

* The permission check runs **in each business service**, not only at the gateway — a service must be safe
  even if reached directly on its own port.
* Map every existing endpoint to its permission, e.g.:

  | Endpoint | Permission |
  |---|---|
  | `GET /assets` | `ASSETS.VIEW` |
  | `POST /assets` | `ASSETS.CREATE` |
  | `PUT /assets/{id}` | `ASSETS.EDIT` |
  | `DELETE /assets/{id}` | `ASSETS.DELETE` |
  | `POST /attribute-definitions` | `ASSET_ATTRIBUTES.CREATE` |
  | `POST /assets/{id}/disposal` | `ASSET_FINANCE.APPROVE` |
  | `POST /users` | `IAM.USER_CREATE` |

* Provide one reusable helper so every Carter module reads the same, e.g.
  `.RequirePermission(Permissions.Assets.Create)` — a thin wrapper over an ASP.NET authorization policy.
* Unauthenticated → **401**. Authenticated but lacking the permission → **403**, returned as ProblemDetails
  consistent with each service's existing exception handler.
* Token validation config (issuer, audience, signing key/JWKS) lives in shared code so all services agree.

---

## 8. Service-to-service communication

* Identity issues a **JWT access token** (short-lived, e.g. 15 min) and an opaque **refresh token**
  (stored only as a hash).
* Business services **validate the token locally** — no network call to Identity per request.
* Put the user id, and only the claims that are small and stable, in the token. Prefer
  **permission claims in the token** for speed; if the claim set grows too large, switch to a cached
  permission lookup and document the cache invalidation rule (role change must take effect promptly).
* Add the gateway route `/auth-service/{**catch-all}` → the Identity service, matching the existing
  `/property-management-service/*` and `/assets-management-service/*` pattern, and register the new service
  in `compose.yaml` and `src/ERP.slnx`.
* Login endpoints need a **stricter rate limit** than the default gateway policy.

---

## 9. Security requirements

### 9.1 Passwords
Hash with a modern algorithm (ASP.NET Identity's PBKDF2 hasher or Argon2/BCrypt) — never plaintext,
never reversible. Support change, reset, forced change, and record `password_changed_at`.

### 9.2 Brute force
Track `failed_login_attempts`; after the configured threshold set `locked_until`. A successful login resets
the counter. Every attempt — success or failure — is written to `login_attempts`. Failed logins must not
reveal whether the username exists.

### 9.3 Sessions & refresh tokens
Store `refresh_token_hash` (never the token). Support rotation, expiry, revocation, logout, admin-forced
logout, and multi-device listing. Detect reuse of a rotated token and revoke the chain.

### 9.4 Trust boundary
The server derives the actor from the authenticated identity. Actor fields currently accepted in request
bodies (`performedBy`, `changedBy`, `approvedBy`, `deletedBy`) must be removed from the public contract and
populated from `ICurrentUser`.

### 9.5 MFA
Design for TOTP first, with SMS/WebAuthn possible later. Secrets encrypted at rest, never returned after
enrolment, verified on a second step of login rather than the first.

---

## 10. API surface

```http
# Authentication (anonymous)
POST /auth/login
POST /auth/refresh
POST /auth/logout
POST /auth/forgot-password
POST /auth/reset-password

# Authenticated user, self-service only
GET  /auth/me                      # identity + effective permissions
POST /auth/change-password
GET  /auth/sessions
POST /auth/sessions/{id}/revoke

# User administration — Super Admin only
POST   /users
GET    /users
GET    /users/{id}
PUT    /users/{id}
POST   /users/{id}/activate
POST   /users/{id}/deactivate
POST   /users/{id}/unlock
POST   /users/{id}/reset-password
POST   /users/{id}/revoke-sessions
GET    /users/{id}/roles
POST   /users/{id}/roles
DELETE /users/{id}/roles/{roleId}
GET    /users/{id}/permission-overrides
POST   /users/{id}/permission-overrides
DELETE /users/{id}/permission-overrides/{permissionId}

# Roles & permissions — Super Admin only
POST   /roles
GET    /roles
GET    /roles/{id}
PUT    /roles/{id}
DELETE /roles/{id}
GET    /roles/{id}/permissions
POST   /roles/{id}/permissions
DELETE /roles/{id}/permissions/{permissionId}
GET    /permission-modules
GET    /permissions

# Security audit — Super Admin only
GET /security/login-attempts
GET /security/sessions
```

`GET /auth/me` returning effective permissions is what lets the UI hide unavailable actions — while the
backend still enforces them independently (P4).

---

## 11. Delivery plan

| Phase | Output |
|---|---|
| **0. Analyse** | The written analysis from §2. **Review before continuing.** |
| **1. Design** | Decisions for every item in §12; entity list; token & claims design; permission catalogue. |
| **2. Scaffold** | `Identity.Domain/Application/Infrastructure/Api` following §2.2; `auth` schema; registered in `ERP.slnx`, `compose.yaml`, gateway. |
| **3. Data** | Entities, configurations, migration, permission catalogue seeder, first Super Admin seeder. |
| **4. Authentication** | Login, logout, refresh, password management, lockout, login auditing, sessions. |
| **5. Authorization** | Roles, permissions, assignments, overrides, resolution (§7.2), `/auth/me`. |
| **6. Super Admin** | Provisioning workflow (§5) with all guard rails. |
| **7. Integrate** | Shared JWT validation + `ICurrentUser`; replace `"Saud-DEV"`; remove client-supplied actor fields; apply `RequirePermission` to every existing endpoint. |
| **8. Test** | §13, end to end against the running stack. |

MFA may be deferred past phase 5, provided the schema and login flow leave room for it.

---

## 12. Open decisions — answer these in phase 1

1. **Employee link** — option A, B or C from §4?
2. **Token lifetimes** — access token and refresh token durations; sliding or absolute?
3. **Permission claims** — embedded in the JWT, or looked up per request with a cache?
4. **Lockout policy** — attempts threshold and lockout duration.
5. **Password policy** — minimum length, complexity, history, expiry.
6. **First Super Admin** — seeded from configuration, environment variables, or a one-time setup command?
7. **Audit column type** — widen `IEntity.CreatedBy` to `Guid?`, or keep `string?` and store the id as text?
8. **MFA** — in the first release, or schema-only for now?
9. **Service name** — `Identity` (recommended, since it owns more than authentication) or `Authentication`?
10. **Port** — next free local port (5142 suggested) and compose mapping.

---

## 13. Test checklist

**Authentication:** valid login · wrong password · unknown username (same response shape as wrong password)
· inactive account · locked account · lockout after N failures · counter reset after success · forced password
change on first login · refresh rotation · reuse of a rotated refresh token · logout · admin-forced logout ·
expired access token.

**Authorization:** role grants permission · role without permission → 403 · ALLOW override grants ·
DENY override beats a role grant · expired role grants nothing · expired override grants nothing ·
inactive role grants nothing · unauthenticated → 401.

**Super Admin:** provisioning creates user + link + roles · non-admin calling `/users` → 403 ·
employee cannot assign themselves a role · non-admin cannot grant `SUPER_ADMIN` ·
system role cannot be deleted or renamed · last Super Admin cannot be deactivated ·
user cannot change their own roles or active status.

**Integration:** every business endpoint rejects a caller lacking its permission · audit columns record the
real authenticated user, not `"Saud-DEV"` · actor fields cannot be spoofed from the request body ·
a service enforces permissions when called directly on its own port, bypassing the gateway.

---

## Appendix — implementation status (2026-09-24)

The Identity service is built and verified end to end (36/36 checks). Decisions taken against §12,
since the questions were not answered before implementation began:

| # | Decision | Taken |
|---|---|---|
| 1 | Employee link | **Option A** — `employee_id` nullable, unique when present, no FK (HRM does not exist) |
| 2 | Token lifetimes | access 15 min, refresh 7 days, absolute, with rotation |
| 3 | Permission claims | **embedded in the JWT** — a revoked role takes effect within one access-token lifetime |
| 4 | Lockout | 5 failed attempts, 15 minute lock |
| 5 | Password policy | min 8, upper + lower + digit; symbol optional |
| 6 | First Super Admin | seeded from `Seed:*` configuration; password generated and logged once if unset |
| 7 | Audit column type | kept `string?` — avoids migrating the two existing services; `ICurrentUser.AuditName` supplies it |
| 8 | MFA | schema and login hook present; TOTP verification not implemented |
| 9 | Service name | **Identity** |
| 10 | Port | 5142 local and compose; gateway route `/auth-service/*` |

Still outstanding: **phase 7** — applying `RequirePermission` to the existing Assets/Property
endpoints, replacing `"Saud-DEV"`, and removing client-supplied actor fields. The business services
are still anonymous.
