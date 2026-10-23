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
  - `EmployeeId`: the HRM employee record, no FK. **`EmployeeCode`**: HR's employee number (`EMP-101`), set together
    with it by `LinkEmployee` (`PUT /users/{id}/employee-link`); unique among linked accounts. Identity issues no codes
    (the template was removed 2026-10-20, migration `UserSearchLockoutWindowHrNumbers`); an older code on an unlinked
    account is shown as "old" and cleared when the account is unlinked.
  - Generated column `search_text` (lower username, email, code, name) behind the Users search.
  - **`IsAuthorizedOfficer`**: the DG's designation under GDA Act s.2(a-i)/28/30. It is a designation, not a
    role, and can't be set on an inactive account.
  - Lockout fields (`FailedLoginAttempts`, `LastFailedLoginAt` - failures older than the lockout window are forgotten,
    `LockedUntil`), `MustChangePassword`, last login, and soft delete (`DeletedAt/By`; `Restore()` brings it back switched off).
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
  - The actor and target names are snapshotted as DISPLAY names (older rows were rewritten 2026-10-20), so a row
    still reads after a rename or deletion.
  - Only changes are recorded, not views. A save that changes nothing (same settings, same role details, same
    permissions) records nothing.
  - The detail says WHAT changed, in two fixed shapes the Administration screens parse (`Application/Abstractions/
    ActivityDetail.cs`): field changes `Name: Clerk → Senior clerk; Active: Yes → No`, and permissions
    `+PROPERTY:VIEW,CREATE;ASSETS:* -HR:DELETE` (`*` = every action of that module). Overrides read
    `PROPERTY.VIEW · Allow`. The recorder cuts a detail at 500 characters (the column size).

## Auth flow

- **Login** (`Features/Auth/Commands/Login`) accepts a username or an email.
  - A wrong password or unknown user gets **401** with a generic message (an unknown user is checked against a dummy
    hash, so it takes as long). The real reason goes only to the log and `login_attempts` ("Wrong password", "Unknown
    username", "Account locked", "Account deactivated", "Account deleted").
  - A **locked** account answers **403** "locked until …" BEFORE the password is looked at, and a switched-off or deleted
    one answers like a wrong password (401) - so no answer ever tells a guesser the password was right (until 2026-10-21
    a right password on a locked account answered 403 and a wrong one 401: the lockout did not stop guessing).
  - At most 60 sign-in attempts a minute per address (`RequireRateLimiting("sign-in")`, 429): password spraying across
    accounts, which the per-account lockout cannot see.
  - Sessions that ended more than 30 days ago are purged at sign-in.
- **JWT** (HS256, `Infrastructure/Security/JwtTokenService.cs`). Claim names are in
  `BuildingBlock.Authentication/ErpClaimTypes.cs`:
  - `sub`, `jti`, `username`, `name` (display name - every service's audit trail writes it), `email`, `sid`
  - **`ast`**: the account's access stamp (see "Token freshness")
  - **`stn`** / **`sof`**: one claim per town / HRM office the person works on (data scope); none = everywhere
  - `emp`, only if linked to an employee
  - **`pwd`** = `"change"` while the account holds a temporary password: such a token carries NO `perm` and NO `role`
    claims, so it opens only the caller's own `/auth/*` routes (`RequireSignedIn` refuses it too)
  - **`ao`** = `"true"`, only for authorized officers
  - one **`role`** claim per role and one **`perm`** claim per effective permission
- **Lifetimes** come from the security settings (defaults: access token 15 min, refresh token 7 days). The refresh
  token SLIDES: each rotation issues a full new lifetime, so the setting reads "sign people out after N days without use".
  Validators allow 30 s clock skew.
- **Refresh rotates on every call.** Reusing an already-rotated refresh token revokes that sign-in's chain (the
  successors) and returns 401; a reuse within 30 s of the rotation (a lost answer, a double click) is refused without
  revoking anything.
- **Token freshness** (since 2026-10-21): every service checks each token with Identity (`GET /auth/token-state`,
  cached 30 s per session + stamp; `BuildingBlock.Authentication/TokenState.cs`). A token stops working when its sign-in
  ends (logout, revoke, theft, password change/reset, deactivation) or when the account's `access_stamp` moved: roles,
  exceptions, the access of a role it holds, scope, activation, deletion, HR link, authorized officer, password all call
  `User.AccessChanged()` / `AccessStamps.*`. The 401 carries `X-Token-Stale: <reason>`; the frontend refreshes once and
  gets a token with the new access. Identity checks its own tokens from the database (`DatabaseTokenStateChecker`); a
  rotated session lives on in its successor. If Identity does not answer, other services let the token through (logged).
- **Longest sign-in**: a session carries `signed_in_at` through every rotation; past `SignInMaxDays` (default 30) the
  refresh is refused and the password asked again.
- **Logout** is anonymous, takes the refresh token, and is idempotent. `AllSessions=true` revokes every session.
- **Passwords**:
  - Hashed with ASP.NET Core `PasswordHasher` (PBKDF2).
  - Policy from the security settings (defaults: minimum 8 characters, at least one upper, lower and digit),
    applied by `PasswordPolicy`.
  - Generated passwords are 14+ characters and returned once.
- **Lockout** from the security settings (default: 5 failures within 15 min lock the account for 15 min). Wrong
  current passwords at change-password count too. Admin reset and unlock clear it.
- **Must change password** is enforced by the token (`pwd` claim, no permissions). Every new account must change its
  first password, typed or generated.
- **Change password** revokes all of the caller's sessions and writes `PasswordChanged`; wrong current passwords that lock
  the account write `AccountLockedByPasswordChange`. Logout writes `SignedOut`, "sign out my other devices"
  `SignedOutOtherDevices`, a reused refresh token `SessionTheftDetected` (actor "System · Sign-in protection").

## Authorization model

- **Catalog**: `src/BuildingBlocks/BuildingBlock.Authentication/PermissionCatalog.cs`. It holds constants
  (`PermissionCatalog.Property.Approve` = `"PROPERTY.APPROVE"`) and the registry the seeder uses (`AllModules`,
  `BuildAll()`).
- Modules: ASSETS, ASSET_TAXONOMY, ASSET_ATTRIBUTES, ASSET_FINANCE, INVENTORY, PROCUREMENT, PROPERTY,
  PROPERTY_SETUP, PROPERTY_LEGAL, IAM_USERS, IAM_ROLES, IAM_PERMISSIONS, IAM_SECURITY, IAM_AUDIT, IAM_SYSTEM, HR,
  HR_SETUP, HR_SENSITIVE, ATTENDANCE, PAYROLL, PAYROLL_SETUP, TAX, TAX_SETUP, DASHBOARD.
- Actions (`Domain/Enums/PermissionAction.cs` mirrors `PermissionCatalog.Actions`; a code whose action is not in the
  enum is SKIPPED by the seeder): VIEW CREATE EDIT DELETE APPROVE POST ASSIGN REVOKE EXPORT RUN RESET DISABLE OVERRIDE
  REVERSE WAIVE ISSUE ADJUST PAY. One code is one act an owner may give or withhold on its own (reversing a payment is
  TAX.REVERSE, not "approve").
- `PermissionCatalog.InheritedFrom`: a code split off an older one is given, ONCE when the seeder first creates it, to
  every role holding the older code (one `PermissionsSeeded` line, actor "System · Startup").
- `PermissionCatalog.Conflicts`: pairs one person should not hold together. `PUT /roles/{id}/permissions` and
  `PUT /users/{id}/roles` refuse a save that NEWLY puts a pair together (400, message starts `CONFLICT:`) unless it
  carries `conflictReason`, which is written as `ConflictAccepted`. SUPER_ADMIN is exempt.
- **Duty rules** (`BuildingBlock.Authentication/DutyRules.cs`): maker-checker rules every service enforces on the act
  itself (`IDutyRules.SeparationRefusalAsync`). ON unless a Super Administrator switched one off with a reason
  (`duty_rules` table, `DutyRuleSwitched` line). Services read `GET /duty-rules/state` (anonymous, cached 1 min,
  unknown = ON).
- **Data scope**: `user_scopes` (Town / Office rows; none of a kind = everywhere), set by `PUT /users/{id}/scope`,
  carried as `stn` / `sof` claims, read as `ICurrentUser.TownScope` / `OfficeScope`; each service filters by it.
- **Effective permissions** (`Domain/Services/PermissionResolver.cs`), in order of precedence:
  1. A live **DENY** override wins.
  2. Otherwise a live ALLOW override grants the permission.
  3. Otherwise a live role grant gives it. The grant must be unexpired, and its role active and not deleted.
  4. Otherwise the permission is denied.
- **Guards** (`Application/Services/IdentityGuard.cs`). Call them in every admin handler:
  - `EnsureNotSelf`: nobody changes their own roles, overrides, active status, authorized-officer flag, password or
    deletion from Administration. Self-service goes through `/auth/*`.
  - `EnsureCanActOnAsync`: only a Super Administrator acts on a Super Administrator's account, and NOBODY acts on an
    account that can do something they cannot (reset, edit, unlock, activate, delete, sign out, roles, exceptions, link,
    restore, scope) - the takeover-by-reset hole closed on 2026-10-21.
  - Guard refusals throw `AccessRefusedException`; `IdentityExceptionHandler` writes each as an `ActRefused` line in its
    own unit of work, so an administrator sees who tried.
  - Linking an HR record to your OWN account is refused (it would make you that employee in HRM self-service).
  - Updating a role: not one you hold, only a Super Administrator touches SUPER_ADMIN, switching a role back on needs
    the access it carries; switching it off or on re-issues every holder's tokens. Lifting a Deny exception needs the
    permission it denied.
  - `EnsureCanGrantAsync` / `EnsureCanGrantRolesAsync`: nobody gives access they do not hold (roles given to an
    account, permissions put on a role, Allow exceptions; Super Administrators excepted).
  - `EnsureNotOwnRoleAsync`: nobody rewrites what a role they hold can do.
  - `EnsureCanAdministerRole`: only a caller holding SUPER_ADMIN may grant, remove or edit SUPER_ADMIN.
  - `EnsureNotLastSuperAdminAsync`: at least one usable SUPER_ADMIN must remain.
  - Creating a role with permissions needs IAM_PERMISSIONS.ASSIGN; creating an account with roles needs IAM_USERS.ASSIGN.
  - Role codes are made by the server from the name (`RoleCodes.FreeCodeForAsync`) when none is sent; a deleted role's
    code is never reused. Deleting a role refuses while someone holds it now, naming them.
- **Other services** call `services.AddErpAuthentication(configuration)` and `.RequirePermission(PermissionCatalog.X.Y)`.
  The policy checks the `perm` claim; the token itself is checked with Identity (token freshness). `.RequireAuthorization()`
  (the default policy) refuses a temporary-password token everywhere; Identity's `/auth/me`, `/auth/change-password` and
  `/auth/token-state` use `TemporaryPasswordAllowedPolicy`. `X-Forwarded-For` from private networks is honoured, so the
  address recorded is the person's, not the gateway's.
  `ICurrentUser` gives `UserId`, `Roles`, `Permissions`, `IsAuthorizedOfficer`, `HasPermission()` and `AuditName`.

## API (permission per route)

| Routes | Permission |
| --- | --- |
| `POST /auth/login`, `/auth/refresh`, `/auth/logout`, `GET /auth/password-policy` | anonymous |
| `GET /auth/me` (permissions resolved live, with `roleNames`), `POST /auth/change-password`, `GET /auth/sessions` (ended ones: real endings only, last 50), `POST /auth/sessions/{id}/revoke`, `POST /auth/sessions/revoke-others` | any valid token |
| `GET /directory/users?ids=|search=` (names only), `GET /attention` (`users.locked`, `users.temporary-password`, `users.no-access`, `roles.ending`, `signins.failed` - each only for who may act) | any signed-in person (`RequireSignedIn`) |
| `POST /users` (roles need `IAM_USERS.ASSIGN` too) | `IAM_USERS.CREATE` |
| `GET /users` (search part of name/username/email/code; `status` = active, locked, temporary-password, never-signed-in, no-access, role-ending, deactivated, deleted; `roleId` = holds it now; `sort` = name, username, lastSignIn, registered; `direction`; pages ≤ 200), `GET /users/summary`, `GET /users/{id}?includeDeleted=` | `IAM_USERS.VIEW` |
| `PUT /users/{id}` (name and email), `PUT /users/{id}/employee-link` | `IAM_USERS.EDIT` |
| `POST /users/{id}/reset-password` | `IAM_USERS.RESET` |
| `POST /users/{id}/activate|deactivate|unlock` | `IAM_USERS.DISABLE` |
| `DELETE /users/{id}` (soft), `POST /users/{id}/restore` | `IAM_USERS.DELETE` |
| `PUT /users/{id}/roles` (exact set, end dates of kept roles too, `conflictReason`; one `RolesChanged` line), `POST /users/{id}/roles`, `DELETE /users/{id}/roles/{roleId}`, `POST /users/{id}/authorized-officer`, `PUT /users/{id}/scope`, `POST /access-reviews`, `PUT /access-reviews/{id}/decisions/{userId}`, `POST /access-reviews/{id}/close` | `IAM_USERS.ASSIGN` |
| `GET /oversight`, `GET /access-report`, `GET /access-reviews` | `IAM_USERS.VIEW` |
| `POST/DELETE /users/{id}/permission-overrides…` | `IAM_PERMISSIONS.OVERRIDE` |
| `POST/DELETE /roles/{id}/permissions…`, `PUT /roles/{id}/permissions` (exactly this list afterwards, `conflictReason`; one `RolePermissionsChanged` line; SUPER_ADMIN may gain but never lose) | `IAM_PERMISSIONS.ASSIGN` |
| `GET /permission-modules`, `GET /permissions` | `IAM_PERMISSIONS.VIEW` |
| `POST/GET/PUT/DELETE /roles…` | `IAM_ROLES.*` |
| `GET /security/login-attempts` (`reason` = wrong-password, unknown-username, locked, deactivated, deleted), `GET /admin-activity` (newest first; filter by actor, target, `personId` = actor or target, action, `kind` = accounts / access / roles / settings - an unknown kind is a 400 -, date; a date without a zone is UTC), `GET /audit`, `GET /audit/kinds` (Administration's acts in the all-modules audit shape, module "Administration") | `IAM_AUDIT.VIEW` |
| `POST /audit/exports` (an Administration export noted as `Exported`) | `IAM_AUDIT.EXPORT` or `IAM_USERS.EXPORT` |
| `GET /users/{id}/sessions`, `GET /sessions` (everyone signed in now), `GET /security-settings`, `GET /duty-rules` | `IAM_SECURITY.VIEW` |
| `PUT /security-settings`, `PUT /security-settings/oversight` (dormant days, automatic switch-off, review months, longest sign-in) | `IAM_SECURITY.EDIT` |
| `POST /users/{id}/revoke-sessions`, `POST /sessions/{id}/revoke` | `IAM_SECURITY.REVOKE` |
| `PUT /duty-rules/{code}` `{isOn, reason}` | Super Administrator (role) |
| `GET /system/status` | `IAM_SYSTEM.VIEW` |
| `GET /duty-rules/state` (codes + on/off, read by every service) | anonymous |
| `GET /auth/token-state` (every service asks it; 200 = current, 401 + `X-Token-Stale` = not), `GET /permissions/conflicts` | any valid token |

Errors (`Api/IdentityExceptionHandler.cs`):
- 401: invalid credentials
- 403: account unavailable
- 400: validation or domain error
- 404: not found
- 409: DB update or concurrency conflict (a plain sentence - never the database's own text)
- 500: a plain sentence (the exception goes to the log only)
- `Result.Failure`: 400 with `{ message }`

## Seeding (`Infrastructure/Data/Seed/IdentitySeeder.cs`, `seed.json`, section `Seed`)

The seeder is idempotent and runs on every start:
1. Inserts any missing module or permission from `PermissionCatalog`, keeps names in step, and switches OFF a permission
   the catalogue no longer has (kept for history; no role grants it any more). A newly created code with an
   `InheritedFrom` entry goes to every role that held the older code.
2. Creates `SUPER_ADMIN` if missing and gives it **every** permission, including ones added later.
3. Creates the first admin if no live SUPER_ADMIN exists. Outside Development the configured password is ignored:
   a random one is logged once and must be changed. Override with `Seed__SuperAdminPassword`.
4. Writes the security settings the first time.
5. Creates the security-settings row from config (`Security` + `Jwt` lifetimes), **only the first time**. After
   that, edits made in the app win, and changing appsettings no longer changes the policy.

`Seed:Enabled=false` turns seeding off.

## Infrastructure and config

- EF Core with Npgsql, snake_case, enums stored as strings, and one configuration class per entity. Query filters
  hide soft-deleted users and roles. Audit columns are stamped from `ICurrentUser.AuditName`.
- Migrations: `InitialCreate`, `AddEmployeeCode`, `AddAuthorizedOfficer`, `RemoveMfa`, `AddSecuritySettings`,
  `AddAdminActivity`, `UserSearchLockoutWindowHrNumbers` (search column, lockout window, HR-number index, drops the
  employee-code template), `AdminOversight` (access stamp, session `signed_in_at`, oversight settings, `user_scopes`,
  `duty_rules`, `access_reviews` + decisions, nullable activity actor = the system, detail 2000 characters). `DatabaseInitializer` waits for the
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
2. Call `IdentityGuard` for any admin action on a user or role; if it changes what someone may do, raise the access stamp
   (`AccessStamps`) in the same unit of work.
3. **Record every administrative change** with `IActivityRecorder.RecordAsync(...)` before saving. It joins the
   same transaction, so a rolled-back change logs nothing. Add a value to `Domain/Enums/ActivityAction.cs` if needed
   (and to its kind in `ActivityKinds`), and write the detail with `ActivityDetail` so the screens can read it.
4. Map the route in `Api/Endpoints/<Area>/` with `.RequirePermission(...)`. Use `.RequireAuthorization()` for
   self-service and `.AllowAnonymous()` only for getting tokens.
5. Read IP and user agent from `HttpContext` (`CallerIp()`, `CallerUserAgent()`) and the actor from
   `ICurrentUser`, never from the request body.

## Gotchas

- **Access changes act within 30 seconds** (token freshness), but only if the handler raised the stamp: a new act that
  changes what someone may do must call `User.AccessChanged()` / `AccessStamps.AccessChangedAsync` /
  `HoldersAccessChangedAsync` before saving, or tokens keep the old access until they expire.
- **A role grant that runs out by its date** does not raise the stamp: the old token keeps the role until it expires
  (at most the access-token lifetime).
- **Oversight** (`Features/Oversight`): `AccessMap` builds every account's access with its sources in one pass (no
  per-user queries) - use it for reports, never `ResolveAsync` in a loop. `DormantAccountsService` (Api) switches dormant
  accounts off twice a day when `SwitchOffDormant` is on - never a Super Administrator.
- **`MapInboundClaims = false`** in `BuildingBlock.Authentication/AuthenticationExtensions.cs` is required. Without
  it .NET renames `role` and `sub` to long URIs, `ICurrentUser.Roles` comes back empty, and no Super Admin could grant
  SUPER_ADMIN. That bug existed until the Property module work.
- **Deleted users still reserve their username, email and HR link** (uniqueness checks ignore query filters) - restore
  the account (`POST /users/{id}/restore`) rather than registering the person again.
- **Not built** (in the requirements doc but not in code): forgot-password, self reset-password,
  `GET /users/{id}/roles`, `GET /users/{id}/permission-overrides`, `GET /roles/{id}/permissions`,
  and `GET /security/sessions`. MFA was never built, and its unused columns were removed.
