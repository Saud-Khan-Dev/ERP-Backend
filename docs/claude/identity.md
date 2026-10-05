# Identity service — context for AI assistants

Read [overview.md](overview.md) first. This file covers `src/Services/Identity`. Original requirements are in
[`docs/auth-service-requirements.md`](../auth-service-requirements.md). Some routes listed there were never
built; see "Not built" below.

## What it is

Authentication and role-based access control (RBAC) for the whole ERP. It issues the JWTs that every other
service validates. There is no self-registration: an administrator creates accounts.

- Port: `http://localhost:5142` (https 5143). In compose it is service `identity-api`, `5142:8080`.
- Through the gateway: `/auth-service/{**}`. The service itself has no route prefix.
- Database: `ERP_DB`, schema **`auth`** (history table `auth.__EFMigrationsHistory`), snake_case.
- Dev login: **`superadmin` / `SuperAdmin@123`** (from `Identity.Api/seed.json`, honored only in Development).

## Domain model (`Identity.Domain/Models`)

All ids are Guid-backed strongly typed ids; `.Of(guid)` rejects `Guid.Empty`.

- **User**:
  - username: lower-cased, 3–100 chars of `[a-z0-9._-]`; email: lower-cased.
  - `PasswordHash`, `DisplayName`, `IsActive`.
  - `EmployeeId`: HRM reference, no FK.
  - **`EmployeeCode`**: e.g. `EMP-101`, unique when present.
  - **`IsAuthorizedOfficer`**: the DG's designation under GDA Act s.2(a-i)/28/30. It is a designation, not a
    role, and can't be set on an inactive account.
  - Lockout fields, `MustChangePassword`, last login, and soft delete (`DeletedAt/By`).
  - No MFA: it was removed (migration `RemoveMfa`).
- **Role**: UPPER_SNAKE `Code`, `IsSystem`, `IsActive`, soft delete. `SUPER_ADMIN` is a system role: it can't be
  renamed, deactivated or deleted, and its permissions can't be removed.
- **PermissionModule** and **Permission**: code `MODULE.ACTION`. Action is a closed enum: View, Create, Edit,
  Delete, Approve, Post, Assign, Revoke.
- **UserRole**: optional `ExpiresAt`. Revoking keeps the row (`RevokedAt`), and a partial unique index allows one
  live grant per (user, role).
- **RolePermission**, **UserPermissionOverride**: Allow or Deny, optional expiry and reason.
- **Session**: one per refresh token. Stores only the SHA-256 of the token and keeps the rotation chain
  (`ReplacedBySessionId`).
- **LoginAttempt**: every attempt, including unknown usernames.
- **EmployeeCodeTemplate**: a singleton row with Prefix, Separator, MinimumDigits and NextNumber (default `EMP-###`
  from 1), protected by an xmin row version. `Application/Services/EmployeeCodeService.cs` issues the next free code,
  or validates a typed code against the template and moves the counter past it. A template edit may not re-issue a code
  already in use.
- **SecuritySettings** (`Models/Settings/SecuritySettings.cs`): a singleton row holding the live security policy.
  - Contents: password rules, lockout (failed attempts, minutes), access-token minutes, refresh-token days.
  - Read through `ISecuritySettingsProvider` at sign-in, password change and token issue. Built-in defaults apply
    until the row exists.
  - Values are bounded so the policy can't lock everyone out: password length 6–128, failed attempts 3–20, lockout
    1–1440 min, access token 5–240 min, refresh token 1–90 days.
- **AdminActivity** (`Models/Audit/AdminActivity.cs`, table `admin_activities`): the append-only trail of
  administrative changes.
  - Each row records who did it, the action, and the target type, id and label, plus a readable detail, IP and user
    agent.
  - The actor and target names are snapshotted, so a row still reads after a rename or deletion.
  - Only changes are recorded, not views.

## Auth flow

- **Login** (`Features/Auth/Commands/Login`) accepts a username or an email.
  - A wrong password or unknown user gets **401** with a generic message. The real reason goes only to the log and
    `login_attempts`.
  - A correct password on a locked, inactive or deleted account gets **403**.
- **JWT** (HS256, `Infrastructure/Security/JwtTokenService.cs`). Claim names are in
  `BuildingBlock.Authentication/ErpClaimTypes.cs`:
  - `sub`, `jti`, `username`, `email`, `sid`
  - `emp`, only if linked to an employee
  - **`ao`** = `"true"`, only for authorized officers
  - one **`role`** claim per role and one **`perm`** claim per effective permission
- **Lifetimes** come from the security settings (defaults: access token 15 min, refresh token 7 days, absolute).
  Validators allow 30 s clock skew.
- **Refresh rotates on every call.** Reusing an already-rotated refresh token revokes all of the user's sessions
  and returns 401.
- **Logout** is anonymous, takes the refresh token, and is idempotent. `AllSessions=true` revokes every session.
- **Passwords**:
  - Hashed with ASP.NET Core `PasswordHasher` (PBKDF2).
  - Policy from the security settings (defaults: minimum 8 characters, at least one upper, lower and digit),
    applied by `PasswordPolicy`.
  - Generated passwords are 14+ characters and returned once.
- **Lockout** from the security settings (default: 5 failures lock the account for 15 min). Admin reset and unlock
  clear it.
- **Must change password** is only a flag in the response. The server does not block other calls.
- **Change password** revokes all of the caller's sessions.

## Authorization model

- **Catalog**: `src/BuildingBlocks/BuildingBlock.Authentication/PermissionCatalog.cs`. It holds constants
  (`PermissionCatalog.Property.Approve` = `"PROPERTY.APPROVE"`) and the registry the seeder uses (`AllModules`,
  `BuildAll()`).
- Modules: ASSETS, ASSET_TAXONOMY, ASSET_ATTRIBUTES, ASSET_FINANCE, INVENTORY, PROCUREMENT, PROPERTY,
  PROPERTY_SETUP, IAM_USERS, IAM_ROLES, IAM_PERMISSIONS, IAM_SECURITY, HR.
- **Effective permissions** (`Domain/Services/PermissionResolver.cs`), in order of precedence:
  1. A live **DENY** override wins.
  2. Otherwise a live ALLOW override grants the permission.
  3. Otherwise a live role grant gives it. The grant must be unexpired, and its role active and not deleted.
  4. Otherwise the permission is denied.
- **Guards** (`Application/Services/IdentityGuard.cs`). Call them in every admin handler:
  - `EnsureNotSelf`: nobody changes their own roles, overrides, active status, authorized-officer flag or deletion.
    Self-service goes through `/auth/*`.
  - `EnsureCanAdministerRole`: only a caller holding SUPER_ADMIN may grant, remove or edit SUPER_ADMIN.
  - `EnsureNotLastSuperAdminAsync`: at least one usable SUPER_ADMIN must remain.
- **Other services** call `services.AddErpAuthentication(configuration)` and `.RequirePermission(PermissionCatalog.X.Y)`.
  The policy only checks for the `perm` claim in the token; there is no call to Identity.
  `ICurrentUser` gives `UserId`, `Roles`, `Permissions`, `IsAuthorizedOfficer`, `HasPermission()` and `AuditName`.

## API (permission per route)

| Routes | Permission |
| --- | --- |
| `POST /auth/login`, `/auth/refresh`, `/auth/logout` | anonymous |
| `GET /auth/me` (permissions resolved live), `POST /auth/change-password`, `GET /auth/sessions`, `POST /auth/sessions/{id}/revoke` | any valid token |
| `POST /users` | `IAM_USERS.CREATE` |
| `GET /users`, `GET /users/{id}`, `GET /employee-code-template` | `IAM_USERS.VIEW` |
| `PUT /users/{id}`, `POST /users/{id}/activate|deactivate|unlock|reset-password`, `PUT /employee-code-template` | `IAM_USERS.EDIT` |
| `DELETE /users/{id}` (soft) | `IAM_USERS.DELETE` |
| `POST /users/{id}/roles`, `DELETE /users/{id}/roles/{roleId}`, `POST /users/{id}/authorized-officer` | `IAM_USERS.ASSIGN` |
| `POST/DELETE /users/{id}/permission-overrides…`, `POST/DELETE /roles/{id}/permissions…` | `IAM_PERMISSIONS.ASSIGN` |
| `GET /permission-modules`, `GET /permissions` | `IAM_PERMISSIONS.VIEW` |
| `POST/GET/PUT/DELETE /roles…` | `IAM_ROLES.*` |
| `GET /users/{id}/sessions`, `GET /security/login-attempts`, `GET /admin-activity` (newest first; filter by actor, target, action, date), `GET /security-settings` | `IAM_SECURITY.VIEW` |
| `PUT /security-settings` (applies from now on; tokens already issued keep their lifetime) | `IAM_SECURITY.EDIT` |
| `POST /users/{id}/revoke-sessions` | `IAM_SECURITY.REVOKE` |

Errors (`Api/IdentityExceptionHandler.cs`):
- 401: invalid credentials
- 403: account unavailable
- 400: validation or domain error
- 404: not found
- 409: DB update or concurrency conflict
- `Result.Failure`: 400 with `{ message }`

## Seeding (`Infrastructure/Data/Seed/IdentitySeeder.cs`, `seed.json`, section `Seed`)

The seeder is idempotent and runs on every start:
1. Inserts any missing module or permission from `PermissionCatalog`. It never deletes any.
2. Creates `SUPER_ADMIN` if missing and gives it **every** permission, including ones added later.
3. Creates the first admin if no live SUPER_ADMIN exists. Outside Development the configured password is ignored:
   a random one is logged once and must be changed. Override with `Seed__SuperAdminPassword`.
4. Creates the default employee-code template.
5. Creates the security-settings row from config (`Security` + `Jwt` lifetimes), **only the first time**. After
   that, edits made in the app win, and changing appsettings no longer changes the policy.

`Seed:Enabled=false` turns seeding off.

## Infrastructure and config

- EF Core with Npgsql, snake_case, enums stored as strings, and one configuration class per entity. Query filters
  hide soft-deleted users and roles. Audit columns are stamped from `ICurrentUser.AuditName`.
- Migrations: `InitialCreate`, `AddEmployeeCode`, `AddAuthorizedOfficer`, `RemoveMfa`, `AddSecuritySettings`,
  `AddAdminActivity`. `DatabaseInitializer` waits for the
  database (12 × 2 s), migrates when `Database:AutoMigrate` is set (default true in Development), then seeds.
- New migration (from `src/`):
  `dotnet ef migrations add <Name> -p Services/Identity/Identity.Infrastructure -s Services/Identity/Identity.Api`.
- Config:
  - `Jwt`: Issuer `erp-identity`, Audience `erp`, `SigningKey`, AccessTokenMinutes 15, RefreshTokenDays 7.
    The dev key is in `appsettings.Development.json`. Startup fails if the key is empty, and **every service must
    use the same key, issuer and audience**.
  - `Security` and the `Jwt` lifetimes: only the **initial** values of the security settings. Change the live
    policy with `PUT /security-settings`.
  - `Seed`.

## Checklists

**New permission for another service**
1. Add the constant to `PermissionCatalog.cs` (an existing module, or a new one in `Modules` + `AllModules`).
2. **Register it in `BuildAll()`.** A constant missing there is never seeded, so nobody can ever be granted it.
3. Use it in the other service with `.RequirePermission(PermissionCatalog.X.Y)`.
4. Restart Identity: SUPER_ADMIN gets it automatically, and other roles get it through `POST /roles/{id}/permissions`.
5. Users receive it with their next token. No migration is needed.

**New Identity endpoint**
1. Add `Features/<Area>/Commands|Queries/<Name>/` with the command or query, a validator and a handler. Return
   `Result<T>` for expected failures, and throw `DomainException` or `*NotFoundException` otherwise.
2. Call `IdentityGuard` for any admin action on a user or role.
3. **Record every administrative change** with `IActivityRecorder.RecordAsync(...)` before saving. It joins the
   same transaction, so a rolled-back change logs nothing. Add a value to `Domain/Enums/ActivityAction.cs` if needed.
4. Map the route in `Api/Endpoints/<Area>/` with `.RequirePermission(...)`. Use `.RequireAuthorization()` for
   self-service and `.AllowAnonymous()` only for getting tokens.
5. Read IP and user agent from `HttpContext` (`CallerIp()`, `CallerUserAgent()`) and the actor from
   `ICurrentUser`, never from the request body.

## Gotchas

- **Token changes are delayed.** Roles, permissions, overrides and the `ao` flag are copied into the token when it
  is issued, so a change reaches a user only with their **next access token** (refresh or login, at most 15 min).
- **Revoking doesn't end access tokens.** Revoking sessions, logout, deactivation and password change don't
  invalidate access tokens already issued: validators don't look up `sid`. Those tokens stay valid until they expire.
- **`MapInboundClaims = false`** in `BuildingBlock.Authentication/AuthenticationExtensions.cs` is required. Without
  it .NET renames `role` and `sub` to long URIs, `ICurrentUser.Roles` comes back empty, and no Super Admin could grant
  SUPER_ADMIN. That bug existed until the Property module work.
- **Deleted users still reserve their values.** A soft-deleted user blocks reuse of their username, email,
  employee id and employee code, because uniqueness checks ignore query filters.
- **Not built** (in the requirements doc but not in code): forgot-password, self reset-password,
  `GET /users/{id}/roles`, `GET /users/{id}/permission-overrides`, `GET /roles/{id}/permissions`,
  and `GET /security/sessions`. MFA was never built, and its unused columns were removed.
