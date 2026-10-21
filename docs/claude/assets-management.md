# Assets Management service — context for AI assistants

Read [overview.md](overview.md) first. This file covers `src/Services/AssetsManagement` after the 2026-10-20 audit fix
(sign-in on every route, the acting person from the token, HR-linked custody, one book value, the field library).

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

It still contains the domain and tables of an **older Inventory, Purchase, Scrap and Warehouse module**; its endpoints
(`/categories`, `/inventoryitems`, `/purchases`) were removed in the audit fix. Leave the rest of the legacy code alone.

- Port: `http://localhost:5139` (https 5140). In compose it is service `assets-management-api`, `5139:8080`, with
  volume `asset-files`.
- Through the gateway: `/assets-management-service/{**}`.
- Database: `ERP_DB`, schema **`assets`**, snake_case. Uses the Postgres extension **`ltree`**.
- **Authentication**: `AddErpAuthentication` + a requirement on EVERY route (table below); a call without a token is 401.
  Compose passes `Jwt__SigningKey` and `Services__HrmBaseUrl` (`http://hrm-api:8080`). The acting person always comes
  from the token (`ICurrentUser`, `CurrentUserExtensions.Actor()`): `performedBy`, `changedBy`, `postedBy`,
  `reversedBy`, `approvedBy`, `recordedBy` are never read from a body, and each is stored with the person's name
  (`*_name` columns) so history reads right after a rename. The audit interceptor writes `ICurrentUser.AuditName`.

| Permission | What it allows |
| --- | --- |
| `ASSETS.VIEW` | the register (`GET /assets`), a record, its files, history, the timeline, the reports' non-money columns |
| `ASSETS.CREATE` | register an asset (`POST /assets`, `GET /assets/next-tag`), upload files while registering |
| `ASSETS.EDIT` | edit details and extra fields, transfer / loan / return, change status, record an event, files |
| `ASSETS.DELETE` | delete a record entered by mistake (with a reason; never a disposed one or one with posted depreciation) |
| `ASSET_TAXONOMY.*` | classes, types, categories and the lists (statuses, locations, currencies, methods, event types) |
| `ASSET_ATTRIBUTES.*` | fields, choice lists, form sections, placements, "not used here" (EDIT) |
| any of ASSETS.VIEW / ASSET_TAXONOMY.VIEW / ASSET_ATTRIBUTES.VIEW | read the structure, the lists, the forms and `GET /field-library` (`AssetsAuthorization.AnyAssetsReader`) |
| `ASSET_FINANCE.VIEW` | every money figure: the register's cost / purchase-date columns and the filters and sorts that read money (refused with 403 without it), acquisition, schedules, book value, valuations, disposal, report money and totals |
| `ASSET_FINANCE.CREATE` | record a purchase (also inside `POST /assets`), start a schedule, write draft months (generate, run without post), add a manual month, record a valuation |
| `ASSET_FINANCE.EDIT` | change a purchase, revise the useful life, stop a schedule |
| `ASSET_FINANCE.POST` | post a month (only once it has ended) and reverse one; `POST /depreciation-runs` with `post: true` |
| `ASSET_FINANCE.DELETE` | delete a draft month or a valuation |
| `ASSET_FINANCE.APPROVE` | record a disposal (the recorder is the approver) and cancel a mistaken one |
| signed in | `GET /attention` (never 403s; lists only the queues the caller may act on) |

## Domain model (`AssetsManagement.Domain/Models`)

- **Taxonomy**: a strict tree, Class → Type → Category → sub-category.
  - `AssetClass`.
  - `AssetType` belongs to one class and carries `IsDepreciable`, `RequiresLocation` and `RequiresCustodian`.
  - `AssetCategory` has a **required** type, an optional parent, an ltree `Path`, `Depth` and `IsLeaf`.
- **Asset** (`Models/Asset/Asset.cs`):
  - Code `AST-000001` (from the Postgres sequence `assets.asset_tag_seq`; read-only after registration), name,
    ownership, class/type/category, status, current location, barcode.
  - Custody: `DepartmentId`/`CustodianId` are HRM's unit and employee ids, checked through HRM's directory with the
    caller's token (`IHrmDirectory`, `HrmDirectoryClient`); a person who left service is refused, a new person brings
    their unit. `DepartmentName`, `CustodianName`, `CustodianNumber` keep the names shown (`CustodyRef`).
  - **`ExtraAttributes`**: a JSONB dictionary that is the source of truth for dynamic field values.
  - Soft delete with `DeletionReason`; deleted assets are never listed and never on a timeline; `xmin` row version.
  - Serial number, RFID tag and parent asset were **removed** in the redesign.
- **Satellites**, each its own aggregate linked by `AssetId`:
  - `AssetAcquisition` (one per asset)
  - `AssetAssignment` (transfer and loan history, with from/to names and who recorded it)
  - `AssetAttachment`
  - `AssetLifecycleEvent` (JSON `Details`)
- **Financial**:
  - `AssetDepreciationSchedule` (`OpeningBookValue`: the cost, or the book value left for a revised one) with
    `AssetDepreciationEntry` children (`PeriodEnd` is exclusive; `LastDay` = the period's last day)
  - `AssetBookValue`: THE book value = cost − every posted, not reversed month ended by a date, across all the
    asset's schedules. The record, the disposal, the reports and `GET /assets/{id}/book-value` all use it.
  - `AssetValuation` (table `asset_valuation_history`)
  - `AssetDisposal` (one per asset)
- **Lookups**, admin-defined (the migration seeds only the system rows below):
  - `AssetStatus` (`IsTerminal`, `AllowsAssignment` - locked once assets use the status)
  - `CurrencyLookup`, keyed by ISO code
  - `Location`, a tree
  - `LifecycleEventType`: SYSTEM types (`IsSystem`, a `Purpose`: Registered, Transfer, Loan, Return, StatusChange,
    Disposal, DisposalCancelled, Valuation, DetailsChanged, DepreciationRevised, DocumentRemoved) are written by the acts
    themselves and found by purpose (`SystemEvents`), never by code; their code and stage are fixed, only the name
    changes. Every other type (inspection, repair) is recorded by hand.
  - `DepreciationMethod` with `Kind` (StraightLine, DecliningBalance, UnitsOfProduction) - the engine keys on the
    kind; the three built-in methods are system rows.
  - `DisposalMethod` (`RequiresValue` = proceeds must be entered, `AllowsValue` = they may be)
- **Dynamic attributes**:
  - `AttributeDefinition` (data type, validation rules, unique-per-category, system flag)
  - `OptionSet` + values
  - `AttributeGroup` (UI sections)
  - `AttributeAssignment`, which binds a definition to exactly one scope: Class, Type, Category or Asset;
    `InheritToChildren` is honoured at every level; `IsHidden` = "not used here" (a field from above left off a node)
  - `AssetAttributeValue`, a typed projection used for filtering
  - `AssetAttributeHistory`

  Domain services in `Domain/Services/DynamicAttribute`:
  - `AttributeSchemaResolver`: resolves Class → Type → Category root…leaf → Asset, where later levels override; a
    level above counts below only with `InheritToChildren`, and a hidden or switched-off placement removes the field.
  - `AttributeValueValidator`: input is a patch, JSON `null` removes a key - but **stored values are never erased**:
    a value whose field is no longer on the form is kept as it is (`GET /assets/{id}` lists them as
    `retiredAttributes`), an unchanged value is not re-checked, and a switched-off choice an asset holds stays valid.
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

- **Create or reclassify an asset**: class and type active, the type in the class, the category an active leaf of both;
  the type's location / custodian requirements met; the starting status active and not final; a tag in the issued
  `AST-` series cannot be typed by hand.
- **Closed records**: a disposed asset (or one in a final status) refuses edits, files, status changes, moves and every
  finance write (`AssetRecordGuard`). A final status is reached only by disposal (`POST /assets/{id}/status` refuses
  one). A loan can always be returned, whatever the status.
- **Statuses**: `IsTerminal`/`AllowsAssignment` cannot change once assets use the status.
- **Categories**: same class and type as the parent, no cycles; a category's type never changes, nor a type's class;
  a leaf that holds assets can't get children.
- **Acquisition**: cost ≥ 0, exchange rate > 0, warranty dates in order, no impossible years.
- **Assignments**: a target is needed; an open loan blocks other moves; custodian / unit checked against HRM.
- **Depreciation**:
  - type depreciable, life 1-1200 months, salvage between 0 and the starting value, a declining rate in (0, 1].
  - Calendar months: the first and last periods take their share of days; the last month takes what is left; never
    below salvage. Declining balance switches to the even share of what is left once that gives more.
  - **A month is posted only once it has ended** (its last day is over, organisation day = Asia/Karachi via `IClock`).
    Posted months are reversed, never deleted.
  - One active schedule per asset (partial unique index `ux_asset_depreciation_schedule_active`); a revision closes
    the running one after its last posted month and starts a new one from the book value left (two saves in one
    transaction because of the index).
- **Disposal**: depreciation is posted up to the disposal date first; book value on the day; gain / loss =
  (proceeds or 0) − book value; proceeds required / allowed by the method and in the purchase currency; refused while
  on loan, before the purchase date, after today. The event is stamped with the real time. Cancelling a mistaken
  disposal (APPROVE, with a reason) restores the earlier status and reopens a stopped schedule; posted months stay.
- **Deleting an asset**: only with a reason; refused for a disposed asset and for one with posted depreciation.
- **Fields**: a field can be deleted only while no (not deleted) asset holds a value; otherwise it is switched off.
- **Attachments**: only an Image can be the primary image; SHA-256; removing one deletes the file too.

## Application conventions

- Each use case lives in `Features/<Area>/<Entity>/{Commands,Queries}/<Name>/` and has two files:
  - `XxxCommand.cs`: input records, the command, the result record and validators.
  - `XxxHandler.cs`.
- Errors:
  - Expected refusals (duplicates, in use) return `Result.Failure(message)`, which gives 400 `{message}`.
  - A missing row throws `XxxNotFoundException` (`Application/Exceptions`), which gives 404.
  - An invariant throws `DomainException`, which gives 400.
- Handlers use `IApplicationDbContext`, with `AsNoTracking` for reference data and **one `SaveChangesAsync` per
  handler**, so each use case is one transaction (the revision uses `BeginTransactionAsync` for its two saves).
- `IClock` (`OrganisationClock`): `UtcNow` and the organisation's `Today`. `IAssetDataQueries`: the tag sequence, tag
  pattern search and JSONB holding checks. `IHrmDirectory`: HRM's staff directory with the caller's token (503 when
  HRM does not answer).
- DTOs: records in `Dtos/<Area>/*Dtos.cs` with hand-written `ToDto()` extensions. Mapster appears only in endpoints
  and legacy code.
- Shared helpers:
  - `AssetCodeIssuer` (sequence), `AssetTaxonomyLoader` and `AssetIdentifierChecks` (in `CreateAssetHandler.cs`)
  - `AssetRecordGuard`, `SystemEvents`, `CustodyResolver`, `FinanceRights` (`Features/Asset/AssetRecordGuard.cs`)
  - `AssetQueryFilter.cs`, used by both the register and the report
  - `Services/AttributeSchemaService.ApplyAttributesAsync`: validates, projects and writes history, but never saves.
    **Call it on every asset create and update, even with no attribute input**, so required fields and defaults apply.

## API

Carter, with **one `ICarterModule` class per endpoint file** under `Api/Endpoints/<Area>/…`. Each route is mapped
directly (no `MapGroup`), with request and response records at the top of the file. A failed `Result` becomes
`Results.BadRequest(new { Message })`. Enums are JSON strings.

- **Assets**:
  - `GET /assets`: the register. Filters (all in `AssetQueryParsing.Read`, shared with the report): assetClassId /
    assetTypeId / categoryId (each alone; includeSubCategories), statusId, locationId (includeSubLocations) or
    noLocation, custodianId / departmentId (HRM ids; departmentId includes sub-units unless includeSubUnits=false) /
    hasCustodian, heldOn (with custodianId), ownership, disposed=include|exclude|only, disposedFrom/To, registeredFrom/To,
    loan=out|overdue, warrantyFrom/To, warrantyWithinDays, outOfService, statusSinceTo, inStatusOverDays,
    missingRequiredLocation, search (part of the tag, name, barcode, description, Searchable fields), attr=code:op:value
    (Filter-flagged fields), sortBy=code|name|registered|acquired|cost|status|location|warranty. Money (cost /
    purchase-date columns; acquiredFrom/To, costMin/Max, hasPurchase, depreciationBehind, sortBy acquired|cost) needs
    ASSET_FINANCE.VIEW: the columns come back empty without it and those filters are 403. HRM's "assets in custody"
    card reads `GET /assets?custodianId=…`.
  - `POST /assets`: the asset with its acquisition and depreciation plan in one transaction (the money parts need
    ASSET_FINANCE.CREATE); the tag is issued from the sequence.
  - `GET /assets/next-tag`, `GET/PUT /assets/{id}` (GET adds `retiredAttributes`), `DELETE /assets/{id}` (body
    `{reason}` or `?reason=`), `PUT /assets/{id}/attributes`, `GET /assets/{id}/attribute-history`,
    `POST /assets/{id}/status`.
- **Per asset**: `…/acquisition`, `…/assignments` (+ `/{aid}/return`; body clearCustodian / clearDepartment),
  `…/attachments` (+ `/{aid}/content`), `…/lifecycle-events` (hand-recorded types only), `…/depreciation-schedules`,
  `…/book-value?asOf=`, `…/valuations` (`valuedByName`), `…/disposal`, `…/disposal/cancel`.
- **Depreciation**: `/depreciation-schedules/{sid}/deactivate {lastDay}|revise|entries|entries/generate|
  entries/{eid}/post|reverse` and `POST /depreciation-runs {until, post, assetId?}`.
- **Register-wide**: `GET /lifecycle-events` (timeline; filters assetId, eventTypeId, stage, from/to, assetClassId,
  categoryId, locationId), `GET /asset-reports/register` (the register's filters or `ids=`, `asOf`, `totalsOnly` -
  money and totals need ASSET_FINANCE.VIEW), `GET /attention`.
- **Taxonomy**: CRUD on `/asset-classes`, `/asset-types`, `/asset-categories`.
- **Fields**: CRUD on `/option-sets` (+ `/values`), `/attribute-groups`, `/attribute-definitions`,
  `/attribute-assignments`; `GET /attribute-schema`; `POST /structure-fields`; `POST /structure-fields/hide`,
  `DELETE /structure-fields/hide/{id}`; `GET /field-library` (every field, its choices, placements, assets holding a value).
- **Lookups**: `/asset-statuses`, `/currencies`, `/locations`, `/lifecycle-event-types`, `/depreciation-methods`,
  `/disposal-methods`.

### `GET /attention` keys (the bell)

Each count is the register's own filter with `disposed=exclude`, so `GET /assets` with the same filter shows the same number.

| Key | Register filter | Listed for |
| --- | --- | --- |
| `assets.loans-overdue` (oldest = earliest missed return date) | `loan=overdue` | ASSETS.EDIT |
| `assets.location-missing` | `missingRequiredLocation=true` | ASSETS.EDIT |
| `assets.out-of-service-long` | `outOfService=true&inStatusOverDays=30` | ASSETS.EDIT |
| `assets.warranty-ending` (nextDue) | `warrantyWithinDays=30` | ASSETS.EDIT |
| `assets.depreciation-behind` | `depreciationBehind=true` | ASSET_FINANCE.POST + VIEW |
| `assets.no-purchase` | `hasPurchase=false` | ASSET_FINANCE.CREATE + VIEW |
| `assets.in-my-custody` (forMe) | `custodianId=<the caller's employee>` | ASSETS.VIEW and a token with `emp` |

Errors (`Api/AssetExceptionHandler.cs`):
- 400: validation (with `ValidationErrors`), domain or bad request; known Postgres constraint violations as sentences
- 401 / 403: no token / not allowed (`AccessDeniedException` for a money filter or a finance act without the right)
- 404: not found
- 409: DB update or concurrency conflict
- 503: HRM's directory did not answer (custody checks)

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
  3. `ActorsCustodyFinanceRules`: name snapshots for every actor and for custody, `deletion_reason`, `is_hidden`,
     `opening_book_value` (= depreciable base + salvage), method `kind`/`is_system`, event type `purpose`/`is_system`
     (existing ones mapped; the missing system ones added; PURCHASED renamed "Registered"), `allows_value`,
     `supplier_name` (supplier id dropped), valuer name, the tag sequence (set after the highest AST- tag), class and
     type placements set to inherit.
  4. `PurgeDeletedE2EAssets`: removes four test assets the end-to-end suites had deleted (matched by id, tag, test
     name and deleted mark - a no-op on any other database).
- `Program.cs` runs `MigrateAsync()` when `Database:AutoMigrate` is set (default on in Development). There is **no
  wait-for-database retry**; the only seed data are the system rows the migration inserts.
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

## Testing

- `src/Tests/AssetsManagement.Domain.Tests` (xUnit): depreciation (calendar months, part months, declining balance,
  posting only ended months, the one book value across schedules), disposal (gain / loss, proceeds rules, dates),
  status locks, loans, custody names, deleting, system event types and methods, choices.
  `dotnet test src/Tests/AssetsManagement.Domain.Tests`.
- `ERP-Frontend/tools/e2e/assets-api.e2e.mjs` drives the whole contract against the running service on standing
  fixtures (type "E2E sandbox" and its categories, found or made and switched off after); each run removes its own
  fields and deletes its assets with a reason. Always pass `assetId` to `POST /depreciation-runs` in tests.

## Gotchas

- Everything is in the global namespace, so type names must be unique across the service. That's why the lookup is
  `CurrencyLookup` and not `Currency`.
- Category and location subtree filters run **in memory** on path prefixes, not as ltree SQL (`AssetQueryFilter.cs`).
- Soft-deleted assets keep their code and barcode, so uniqueness checks use `IgnoreQueryFilters()`. A refused
  registration still uses up a sequence number (tags can skip a number; none is ever reused).
- `Name.Of` throws `ArgumentException`, which becomes a 500, so validators must cap name lengths first.
- `AssetsManagement.Api.http` is fully commented out and outdated. Use `/openapi/v1.json`.
- Legacy Inventory code uses Mapster DTOs and the `Code` value object.
- OpenAPI is mapped in Development only.
