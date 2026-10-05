# Property Management service — context for AI assistants

Read [overview.md](overview.md) first for the stack and shared conventions. This file covers
`src/Services/PropertyManagement` only.

## What it is

The GDA (Galiyat Development Authority) property register: every property GDA owns or manages,
who owns it and in what share, how it is let out (allotment, lease, rental, auction, outsourcing), and
the compliance side (boundaries, encroachments, court cases, departmental appeals, building plans).
Legal basis is the **GDA Act**. Code comments cite its sections (s.6, s.9, s.28-A, s.30, s.32).

**Source of truth for the schema: [`docs/gda_property_module.dbml`](../gda_property_module.dbml).**
The database is built to match it exactly: table names, column names, types, sizes, NOT NULLs, indexes
and uniques. The business rules come from the client's "schema guide" document, which is not in the repo.
They are summarized under "Business rules" below. Do not change the schema unless the user asks; when they do,
update the DBML in the same change.

- Port: `http://localhost:5141` (launchSettings), `5141:8080` in `compose.yaml` (service `poperty-management-api`, typo is real).
- Database: PostgreSQL `ERP_DB`, schema **`property`**, snake_case names.
- OpenAPI: `/openapi/v1.json`. A runnable walkthrough of every main flow:
  `PropertyManagement.Api/PropertyManagement.Api.http`.

## Layout

```text
PropertyManagement.Domain/          entities, value objects, enums, domain rules (no EF, no MediatR)
  Models/Property|Owner|Ownership|Management|Compliance|Document|CustomField|MasterData|CodeSequence
  ValueObjects/  strongly typed Guid ids (PropertyId.Of(guid)), BusinessCode, MasterCode, Name, GeoPoint…
  Enums/         fixed workflow states (ManagementStates.cs etc.)
PropertyManagement.Application/
  Features/<Area>/Commands|Queries/<UseCase>/  one folder per use case: Command/Query + Validator + Handler
  Dtos/            response records + Mappings.cs (entity → DTO)
  MasterData/      MasterRegistry: the generic master-data engine
  Services/        CodeGenerator, DocumentService, EntityLoaders, PropertyReadService, RegisterScope,
                   OpenMatters, ListSorting
  Exceptions/      NotFoundExceptions.cs (also AuthorizedOfficerRequiredException)
PropertyManagement.Infrastructure/
  Data/Configuration/  EF mapping per area, matching the DBML exactly
  Data/Seed/           MasterDataSeed + PropertySeeder (masters + code sequences)
  Data/Migrations/     EF migrations (see below)
  Storage/             LocalFileStorage (documents on disk, SHA-256)
PropertyManagement.Api/
  Endpoints/*.cs       Carter modules, one per area; request records sit at the top of each file
  PropertyExceptionHandler.cs   exception → ProblemDetails status mapping
src/Tests/PropertyManagement.Domain.Tests/   xUnit tests of domain rules (75), in ERP.slnx under /Tests/
```

## Domain model (DBML tables → classes)

- **Property core**: `Property` (PROP-00001), `PropertyStatusHistory`, `PropertyMeasurement`
  (current + superseded), `PropertyAreaRegularization`.
- **Owners**: `PropertyOwner` (OWN-00001; person or organization; CNIC stored as 13 digits, shown
  12345-1234567-1), `OwnerContact`, `OwnerAddress`.
- **Ownership**: `PropertyOwnership` (share %, effective from/to; `AcquiredViaTransferId`,
  `AcquiredViaAllotmentId`), `PropertyTransfer` + `PropertyTransferParty` (TRF-), `PropertyEncumbrance`.
- **Management**: `PropertyAllotment` (ALT-), `PropertyLease` (LSE-), `PropertyRental` (RNT-),
  `AgreementViolation`, `PropertyAuction` + `AuctionBid` (AUC-), `PropertyOutsourcing` (CON-).
- **Compliance**: `PropertyBoundary` + `BoundaryPoint` (lat/long only, **no elevation**;
  `slope_percentage decimal(8,4)`, where 12.5 means 12.5%), `PropertyEncroachment` + `EncroachmentBoundaryPoint`
  (ENC-), `PropertyLitigation` + `LitigationParty` + `LitigationHearing`, `PropertyAppeal` (APL-),
  `BuildingPlan` (BP-, with revisions).
- **Documents**: `PropertyDocument`, which is versioned (`SupersedesDocumentId`) and attached to a property and
  optionally to one record on it (`entity_type` + `entity_id`). `property_id` is **NOT NULL**: an
  owner's paper (CNIC copy) is filed under a property too.
- **Masters**: 27 ERD lookup tables plus `attribute_group`, all subclasses of `MasterData`
  (`Masters.cs`). **Custom fields**: `AttributeDefinition` + `PropertyAttributeValue` (not in the ERD,
  the client asked for configurable fields). **CodeSequence**: numbering templates (not in the ERD).

Ids are Guids wrapped in strongly typed ids. Users live in the Identity service, so the `*_by` columns and
`filed_by_officer_id` hold the Identity user id **without a database FK**. This is intentional, so don't add one.

## Business rules (schema guide numbering; tests are named after them)

1. Current ownership shares on a property never exceed 100%. A DISPUTED ownership doesn't count.
2. History is never overwritten. A status change closes the old row, a new measurement supersedes the old one,
   lease and rental terms are editable only while DRAFT (after that, `renew` creates a new row pointing back),
   and a building plan revision keeps the plan number with `revision_no + 1`.
3. An owner has at most one primary contact and one primary address. Setting a new primary clears the old one.
4. An agreement violation points at **exactly one** agreement (lease, rental or transfer), matching
   its `agreement_type`. The DB enforces this with a check using `num_nonnulls`.
5. The **3rd violation inside the notice period** of the earlier ones cancels the lease or rental.
6. A departmental appeal's decision is due on the appeal date + 120 days. The response warns if the appeal
   was filed more than 30 days after the order was received.
7. Every area is also stored in square feet in `*_base` columns, converted with `measurement_unit.factor_to_base`.
   SQFT is the base unit.
8. Business codes (PROP-00001 …) come from `code_sequence`. Templates are editable (`PUT /code-sequences/{key}`),
   but a template can't move back onto a code already in use.
9. Workflows compare masters by **code**, never by id or name. Codes the code depends on are listed in
   `Domain/Models/MasterData/SystemMasterCodes.cs`. The seeder ensures they exist, and they can't be deactivated.
10. A document attached to a record (entity_type + entity_id) requires that record to exist on that property.

GDA Act rules:
- s.28-A: a fine is at most Rs 1,000,000.
- s.28-A fines and s.30 criminal complaints need an **authorized officer**, the Identity flag
  `is_authorized_officer`. It arrives as JWT claim `ao` and is read through `ICurrentUser.IsAuthorizedOfficer`.
  Without it the API returns 403 (`AuthorizedOfficerRequiredException`). A complaint is recorded against the
  signed-in officer.
- s.6(4)(c): an allotment can be cancelled and restored. Allotment is not ownership until `confirm-ownership`.

## Dynamic master data

`Application/MasterData/MasterRegistry.cs` lists every master with its slug. One generic API serves all of them:
`GET/POST /masters/{slug}`, `PUT /masters/{slug}/{id}`, `POST …/{id}/activate|deactivate`, `GET /masters`.
**Adding a master** takes a class in `Masters.cs`, one line in `MasterRegistry.All`, EF config in
`MasterDataConfiguration.cs`, seed rows in `MasterDataSeed.cs`, and a migration. No endpoints are needed. Code and name
lengths per table come from `MasterLimits`. Seeded values follow the schema guide. Since Saad's change there is
no "Other" value in any master list.

## API map (permissions from `PermissionCatalog`)

| Area | Routes | Permission |
| --- | --- | --- |
| Masters, code sequences, custom-field definitions | `/masters/*`, `/code-sequences`, `/attribute-definitions` | `PROPERTY_SETUP.*` |
| Register | `GET /properties` (filters, sort, paging 1-200), `GET /properties/next-code`, `POST /properties` (can also save owners, boundary and custom fields in one transaction), `/properties/{id}/status`, `/measurements`, `/regularizations`, `/area`, `/attributes` | `PROPERTY.VIEW/CREATE/EDIT` |
| Owners | `/owners` (+ contacts, addresses, documents) | `PROPERTY.*` |
| Ownership | `/properties/{id}/ownerships`, `/transfers` (initiate), `/transfers/completed` (one step), `/transfers/{id}/approve|complete|cancel`, `/encumbrances/*` | approve and complete need `PROPERTY.APPROVE` |
| Management | `/properties/{id}/allotments|leases|rentals|violations|auctions|outsourcing-contracts` + action routes (`/leases/{id}/renew`, `/violations/{id}/fine` …) | official orders need `PROPERTY.APPROVE` |
| Compliance | `/properties/{id}/boundaries|encroachments|litigations|appeals|building-plans` + action routes | official orders need `PROPERTY.APPROVE` |
| Documents | `/properties/{id}/documents`, `/owners/{id}/documents` (multipart, `propertyId` required), `/documents/{id}/versions|content` | `PROPERTY.*` |
| Cross-property lists | `GET /agreements`, `/legal-matters`, `/transfers` (pageSize 1-5000) | `PROPERTY.VIEW` |
| Reports | `GET /property-reports/register` (same filters as `/properties`, max 5000, or picked `ids`), `/property-reports/ownership?asOf=` | `PROPERTY.VIEW` |

Errors are ProblemDetails, mapped in `PropertyExceptionHandler.cs`:
- 400: ValidationException (with `ValidationErrors`), DomainException, bad request
- 403: AuthorizedOfficerRequiredException
- 404: NotFoundException
- 409: concurrency or DB update conflict

Enum query values are case-insensitive (`PropertyQueryParsing.cs`).

## Patterns to follow

- **New use case**: add `Features/<Area>/Commands/<Name>/<Name>Command.cs` (the record, a FluentValidation
  validator and the result record) and `<Name>Handler.cs`. Load aggregates with the `EntityLoaders` /
  `ComplianceLoaders` extension methods (`context.LoadPropertyAsync(id, ct)`, which throws NotFound). Put the rule
  in the **domain method** and keep handlers thin. Return `Result<T>`. Map the route in the area's endpoint file with
  `.RequirePermission(...)`, `.WithName/.WithSummary/.WithDescription`, `.Produces…`, then call `.ToOk()` /
  `.ToCreated(...)`.
- **Domain errors**: `throw new DomainException("message the user can act on")` gives 400. Messages are shown to
  end users, so write them as complete sentences.
- **Enums in the DB**: stored as UPPER_SNAKE text with `HasUpperSnakeEnum()` (`ConversionExtensions.cs`).
  `order_source_table` is lower_snake (`HasLowerSnakeEnum`). Removing an enum value breaks reading existing rows
  that hold it, so add a data migration when you remove one.
- **Audit columns**: only the ones the ERD lists per table. Override `Audit` in the configuration class
  (`AuditColumns.All|Created|CreatedAt|None`, `EntityConfiguration.cs`). The interceptor fills them from `ICurrentUser`.
- **Open or closed matters**: use the shared expressions in `Services/OpenMatters.cs` so filters and lists agree.
  Cross-property lists build on `RegisterScope` (sub-queries, filtered in SQL, active properties only).
- **Codes**: `CodeGenerator` issues the next code and skips any already in use.

## Database and migrations

The service migrates and seeds on startup (`Program.cs` → `DatabaseInitializer`). This is on by default in
Development; set it with `Database:AutoMigrate`. The seeder saves seeds first, then ensures `SystemMasterCodes`.

Migrations:
1. `InitialPropertySchema`
2. `PropertyManagementAndCompliance`
3. `AlignWithErd`: drops audit columns the ERD doesn't have and makes `property_document.property_id`
   NOT NULL. It **stops with an error** if old documents have no property, and the message says how to fix them.

Create migrations from `src/` with
`dotnet ef migrations add <Name> -p Services/PropertyManagement/PropertyManagement.Infrastructure -s Services/PropertyManagement/PropertyManagement.Api`.
EF can't index a complex property, so `ix_property_owner_owner_name` is created with SQL inside a migration.

Config (`appsettings.json`):
- `ConnectionStrings:Database`
- `Jwt:Issuer/Audience/SigningKey`: the key must equal Identity's
- `FileStorage:RootPath`: documents folder; compose mounts the `property-files` volume
- `Documents:MaxFileSizeBytes` (25 MB) and `Documents:AllowedExtensions`

## Testing

- `dotnet test src/ERP.slnx` runs the domain tests. Add a test for every new domain rule, named after the rule or
  Act section. Use `Fixture.cs` helpers (`Master<T>`, `Unit`, `Property`, `Owner`, `D("2026-01-01")`).
- For API behavior, run Identity + Property against Postgres, then step through `PropertyManagement.Api.http`.

## Gotchas

- A change to a user's authorized-officer flag or roles takes effect only with their **next access token**.
- `PropertyId` on a document is required even for owner documents. Upload forms send `propertyId`.
- Transfers move shares: the transferor side and the transferee side must balance. A gift needs `relationship`.
  `/transfers/completed` records a past transfer as COMPLETED in one step, using the same rules.
- An appeal against a fine marks the violation APPEALED. A decided appeal is final.
- The cross-property lists merge several tables in memory after SQL filtering. Keep paging through
  `ListSorting.Page` so ordering stays stable.
- Don't add CHECK constraints, columns or tables that aren't in the DBML. Extra rules belong in the domain.
