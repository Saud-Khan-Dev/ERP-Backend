# Assets Management service — context for AI assistants

Read [overview.md](overview.md) first. This file covers `src/Services/AssetsManagement`, as of the
`assets-redesign` merge (commit `c8c3b43`, "strict structure tree, one-request registration, reports").

## What it is

GDA's fixed-asset register. It covers:
- a classification tree
- dynamic ("structure") fields
- acquisition
- assignments and transfers
- lifecycle events
- depreciation, valuation and disposal
- attachments
- an asset register report

It still contains an **older Inventory, Purchase, Scrap and Warehouse module** (legacy code in a different style).
A teammate's branch that removed it was **not** merged, so leave the legacy code alone unless asked.

- Port: `http://localhost:5139` (https 5140). In compose it is service `assets-management-api`, `5139:8080`, with
  volume `asset-files`.
- Through the gateway: `/assets-management-service/{**}`.
- Database: `ERP_DB`, schema **`assets`**, snake_case. Uses the Postgres extension **`ltree`**.
- **No authentication yet**: endpoints are anonymous, and "who did it" fields (`PerformedBy`, `ApprovedBy`,
  `PostedBy`) come from the request body. Permission codes for this module already exist in `PermissionCatalog`:
  - `ASSETS.*`
  - `ASSET_TAXONOMY.*`
  - `ASSET_ATTRIBUTES.*`
  - `ASSET_FINANCE.*`, which adds POST and APPROVE

  See overview.md for what wiring auth in involves.

## Domain model (`AssetsManagement.Domain/Models`)

- **Taxonomy**: a strict tree, Class → Type → Category → sub-category.
  - `AssetClass`.
  - `AssetType` belongs to one class and carries `IsDepreciable`, `RequiresLocation` and `RequiresCustodian`.
  - `AssetCategory` has a **required** type, an optional parent, an ltree `Path`, `Depth` and `IsLeaf`.
- **Asset** (`Models/Asset/Asset.cs`):
  - Code `AST-000001`, name, ownership, class/type/category, status, department, custodian, current location,
    barcode.
  - **`ExtraAttributes`**: a JSONB dictionary that is the source of truth for dynamic field values.
  - Soft delete; `xmin` row version.
  - Serial number, RFID tag and parent asset were **removed** in the redesign.
- **Satellites**, each its own aggregate linked by `AssetId`:
  - `AssetAcquisition` (one per asset)
  - `AssetAssignment` (transfer and loan history)
  - `AssetAttachment`
  - `AssetLifecycleEvent` (JSON `Details`)
- **Financial**:
  - `AssetDepreciationSchedule` with `AssetDepreciationEntry` children
  - `AssetValuation` (table `asset_valuation_history`)
  - `AssetDisposal` (one per asset)
- **Lookups**, all admin-defined and **not seeded**:
  - `AssetStatus` (`IsTerminal`, `AllowsAssignment`)
  - `CurrencyLookup`, keyed by ISO code
  - `Location`, a tree
  - `LifecycleEventType`
  - `DepreciationMethod`, matched by code: `STRAIGHT_LINE`, `DECLINING_BALANCE`, `UNITS_OF_PRODUCTION`
  - `DisposalMethod` (`RequiresValue`)
- **Dynamic attributes**:
  - `AttributeDefinition` (data type, validation rules, unique-per-category, system flag)
  - `OptionSet` + values
  - `AttributeGroup` (UI sections)
  - `AttributeAssignment`, which binds a definition to exactly one scope: Class, Type, Category or Asset
  - `AssetAttributeValue`, a typed projection used for filtering
  - `AssetAttributeHistory`

  Domain services in `Domain/Services/DynamicAttribute`:
  - `AttributeSchemaResolver`: resolves Class → Type → Category root…leaf → Asset, where later levels override.
    An ancestor category counts only when `InheritToChildren` is set.
  - `AttributeValueValidator`: input is a patch, and JSON `null` removes a key.
  - `AttributeValueProjector`, `AttributeHistoryDiff`.
- **Value objects**:
  - `AssetCode`
  - `LookupCode` (UPPER_SNAKE; `ToPathLabel()` gives the lower-case ltree label)
  - `AttributeCode` (lower_snake; it is the JSONB key)
  - `Name.Of(value, max=100)`
  - `Money`, `Currency`
  - `Code`: legacy `CAT-001` style
- **Enums**:
  - `OwnershipType` {Owned, Leased, Rented, Finance}
  - `AcquisitionType` {Purchase, Foc, Donation, Transfer}
  - `AttachmentType` {Image, Document}
  - `AttributeDataType`, `AttributeScope`

## Business rules (in the domain)

- **Create or reclassify an asset**:
  - The class and type must be active, the type must belong to the class, and the category must match both.
  - The category must be an active **leaf**.
  - The type's location and custodian requirements must be met.
  - The starting status must be active and not terminal.
- **Asset locks**:
  - A deleted asset, or one in a terminal status, can't be edited (`EnsureEditable`). Once an asset is disposed or
    terminal its record is closed (`Features/Asset/AssetRecordGuard.cs`): no attachment uploads or deletes.
  - Nothing leaves a terminal status. Moving into one requires no active depreciation schedule.
  - Assigning requires a status with `AllowsAssignment`.
- **Categories**:
  - A parent must have the same class and type, and there are no cycles.
  - A category's **type can never change**, and neither can a type's class.
  - A leaf that holds assets can't get children.
- **Acquisition**: cost ≥ 0, exchange rate > 0, warranty dates in order.
- **Assignments**: need at least one target. Only a loan (one with an expected return date) can be returned, and
  only once.
- **Depreciation**:
  - The type must be depreciable, the life must be > 0 months, and salvage must be between 0 and cost.
  - `DECLINING_BALANCE` needs a rate in (0, 1].
  - Generated entries are monthly, never go below salvage, and the last period absorbs rounding.
    `UNITS_OF_PRODUCTION` entries are added by hand.
  - Posted entries can't be deleted, only reversed.
  - At most **one active schedule per asset**, enforced by the partial unique index `ux_asset_depreciation_schedule_active`.
- **Disposal**: honors `RequiresValue`; gain or loss = value − net book value.
- **Attachments**: only an Image can be the primary image; the file is hashed with SHA-256.

## Application conventions

- Each use case lives in `Features/<Area>/<Entity>/{Commands,Queries}/<Name>/` and has two files:
  - `XxxCommand.cs`: input records, the command, the result record and validators.
  - `XxxHandler.cs`.
- Errors:
  - Expected refusals (duplicates, in use) return `Result.Failure(message)`, which gives 400 `{message}`.
  - A missing row throws `XxxNotFoundException` (`Application/Exceptions`), which gives 404.
  - An invariant throws `DomainException`, which gives 400.
- Handlers use `IApplicationDbContext`, with `AsNoTracking` for reference data and **one `SaveChangesAsync` per
  handler**, so each use case is one transaction.
- DTOs: records in `Dtos/<Area>/*Dtos.cs` with hand-written `ToDto()` extensions. Mapster appears only in endpoints
  and legacy code.
- Shared helpers:
  - `AssetCodeIssuer`, `AssetTaxonomyLoader` and `AssetIdentifierChecks` (in `CreateAssetHandler.cs`)
  - `AssetQueryFilter.cs`, used by both the register and the report
  - `Services/AttributeSchemaService.ApplyAttributesAsync`: validates, projects and writes history, but never saves.
    **Call it on every asset create and update, even with no attribute input**, so required fields and defaults apply.

## API

Carter, with **one `ICarterModule` class per endpoint file** under `Api/Endpoints/<Area>/…`. Each route is mapped
directly (no `MapGroup`), with request and response records at the top of the file. A failed `Result` becomes
`Results.BadRequest(new { Message })`. Enums are JSON strings.

- **Assets**:
  - `GET/POST /assets`: POST registers the asset with its acquisition and depreciation plan in one transaction and
    issues the AST- tag.
  - `GET /assets/next-tag`.
  - `GET/PUT/DELETE /assets/{id}`, `PUT /assets/{id}/attributes`, `GET /assets/{id}/attribute-history`,
    `POST /assets/{id}/status`.
- **Per asset**:
  - `…/acquisition`
  - `…/assignments` (+ `/{aid}/return`)
  - `…/attachments` (+ `/{aid}/content`)
  - `…/lifecycle-events`
  - `…/depreciation-schedules`, `…/valuations`, `…/disposal`
- **Depreciation**: `/depreciation-schedules/{sid}/deactivate|entries|entries/generate|entries/{eid}/post|reverse`
  and `POST /depreciation-runs`.
- **Register-wide**: `GET /lifecycle-events` (timeline) and `GET /asset-reports/register` (cost, written off, book
  value, latest valuation and disposal, totals per currency).
- **Taxonomy**: CRUD on `/asset-classes`, `/asset-types`, `/asset-categories`.
- **Fields**:
  - CRUD on `/option-sets` (+ `/values`), `/attribute-groups`, `/attribute-definitions`, `/attribute-assignments`.
  - `GET /attribute-schema`.
  - `POST /structure-fields`, which adds a new or existing field to a class, type or category.
- **Lookups**: `/asset-statuses`, `/currencies`, `/locations`, `/lifecycle-event-types`, `/depreciation-methods`,
  `/disposal-methods`.
- **Legacy**: `/categories`, `/inventoryitems`, `/purchases`.

Errors (`Api/AssetExceptionHandler.cs`):
- 400: validation (with `ValidationErrors`), domain or bad request
- 404: not found
- 409: DB update or concurrency conflict

## Infrastructure

- `Data/ApplicationDbContext.cs`: `Schema = "assets"`, `HasPostgresExtension("ltree")`.
- Configurations live in `Data/Configuration/<Area>/` and derive from `EntityConfiguration<TEntity,TId>`, which maps
  the audit columns.
  - Tables are singular `asset_…`.
  - Enums use `HasEnumString()`. JSONB and ltree helpers are in `PostgresPropertyExtensions.cs`.
  - `extra_attributes` has a GIN index.
- Migrations:
  1. `InitialCreate`
  2. `StrictTaxonomyRealUploads`: data SQL that creates a `GENERAL` type, moves categories and assets under types,
     and drops the serial, RFID and parent columns.
- `Program.cs` runs `MigrateAsync()` when `Database:AutoMigrate` is set (default on in Development). There is **no
  wait-for-database retry and no seeding**.
- New migration (from `src/`):
  `dotnet ef migrations add <Name> -p Services/AssetsManagement/AssetsManagement.Infrastructure -s Services/AssetsManagement/AssetsManagement.Api`.
- **Files**: `Infrastructure/Storage/LocalFileStorage.cs` writes under `FileStorage:RootPath` (default `storage`;
  compose uses `/data/asset-files`) at `/asset-files/{AssetCode}/{guid}{ext}`. It writes through a `.partial` temp
  file, computes SHA-256 and guards against path traversal. Limits come from section `Attachments` (25 MB, an
  extension allow-list); the Kestrel multipart limit is 30 MB.

## Adding a feature (copy `Lookup/AssetStatus` → `CreateAssetStatus`)

1. Domain: add an `XxxId` record in `Domain/ValueObjects/<Area>/`, and the entity in `Domain/Models/<Area>/` with a
   static `Create`, private setters and invariants that throw `DomainException`.
2. Add a `DbSet` to both `IApplicationDbContext` and `ApplicationDbContext`.
3. Add `Data/Configuration/<Area>/XxxConfiguration.cs`: call `base.Configure`, then set `ToTable`, the id conversion,
   `HasEnumString`, indexes and FKs with `Restrict`.
4. Add a migration.
5. Add `XxxNotFoundException`, and a DTO with a `ToDto()` extension.
6. Add `Features/<Area>/Xxx/Commands/CreateXxx/CreateXxxCommand.cs` and `CreateXxxHandler.cs`.
7. Add `Api/Endpoints/<Area>/Xxx/CreateXxx.cs`, an `ICarterModule` ending with `.WithName/.Produces/.WithSummary`.

## Gotchas

- Everything is in the global namespace, so type names must be unique across the service. That's why the lookup is
  `CurrencyLookup` and not `Currency`.
- The audit interceptor writes the hard-coded `CreatedBy/LastModifiedBy = "Saud-DEV"`, because there's no current
  user without auth.
- Category and location subtree filters run **in memory** on path prefixes, not as ltree SQL (`AssetQueryFilter.cs`).
  `AssetCodeIssuer` loads every code to find the highest.
- Soft-deleted assets keep their code and barcode, so uniqueness checks use `IgnoreQueryFilters()`.
- Deleting an attachment removes only the DB row; the file stays on disk.
- `Name.Of` throws `ArgumentException`, which becomes a 500, so validators must cap name lengths first.
- `AddAssetAttachment.cs` still mentions an "Other" attachment type; the value was removed from the enum.
- `AssetsManagement.Api.http` is fully commented out and outdated. Use `/openapi/v1.json`.
- Legacy Inventory code uses Mapster DTOs and the `Code` value object. Scrap has handlers but no endpoints, and the
  `Events/Purchase/*.cs` files are empty.
- There are no tests for this service yet.
- The Dockerfile copies only `BuildingBlocks/BuildingBlock/`. Copy all of `BuildingBlocks/` when adding auth.
