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
src/Tests/PropertyManagement.Domain.Tests/   xUnit tests of domain rules (232), in ERP.slnx under /Tests/
```

## Domain model (DBML tables → classes)

- **Property core**: `Property` (PROP-00001), `PropertyStatusHistory`, `PropertyMeasurement`
  (current + superseded), `PropertyAreaRegularization`.
- **Owners**: `PropertyOwner` (OWN-00001; person or organization), `OwnerContact`, `OwnerAddress`. CNIC: 13 digits
  stored 12345-1234567-1, never starting with 0 (`Cnic`). NTN: FBR 7+1 stored 1234567-8, or a 13-digit CNIC-based
  number stored 12345-1234567-1 (`Ntn`); duplicates are compared on the digits. By owner type (system codes):
  INDIVIDUAL needs a CNIC; COMPANY / TRUST / GOVERNMENT / JOINT_ENTITY have no CNIC and no father / husband name.
  There is no registration number.
- **Ownership**: `PropertyOwnership` (share %, effective from/to; `AcquiredViaTransferId`,
  `AcquiredViaAllotmentId`), `PropertyTransfer` + `PropertyTransferParty` (TRF-), `PropertyEncumbrance`.
- **Management**: `PropertyAllotment` (ALT-), `PropertyLease` (LSE-), `PropertyRental` (RNT-),
  `AgreementViolation`, `PropertyAuction` + `AuctionBid` (AUC-), `PropertyOutsourcing` (CON-).
- **Compliance**: `PropertyBoundary` + `BoundaryPoint` (lat/long only, **no elevation**;
  `slope_degrees numeric(6,2)`, 0 to 90 degrees; above 45 the survey is recorded with a Building & Construction
  Regulations 2026 warning, never refused), `PropertyEncroachment` + `EncroachmentBoundaryPoint`
  (ENC-), `PropertyLitigation` + `LitigationParty` + `LitigationHearing`, `PropertyAppeal` (APL-),
  `BuildingPlan` (BP-, with revisions).
- **Documents**: `PropertyDocument`, versioned (`SupersedesDocumentId`). A property paper has `property_id` and
  optionally points at one record on it (`entity_type` + `entity_id`). An owner's own paper (CNIC copy, NTN
  certificate ...) has `entity_type` OWNER, `entity_id` = the owner and **no** `property_id`. `document_type.applies_to`
  (PROPERTY | OWNER) must match: OWNER kinds only on owners, PROPERTY kinds only on property records; a re-type stays
  within the scope. `property_owner.cnic_document_id` = the owner's latest active, latest-version CNIC_COPY paper,
  refreshed by `DocumentService.RefreshOwnerCnicAsync` (domain: `PropertyOwner.RefreshCnicDocument`) after every
  upload, new version, (de)activation and re-type.
- **Masters**: 27 ERD lookup tables plus `attribute_group`, all subclasses of `MasterData`
  (`Masters.cs`). **Custom fields**: `AttributeDefinition` + `PropertyAttributeValue` (not in the ERD,
  the client asked for configurable fields). **CodeSequence**: numbering templates (not in the ERD).

Ids are Guids wrapped in strongly typed ids. Users live in the Identity service, so the `*_by` columns and
`filed_by_officer_id` hold the Identity user id **without a database FK**. This is intentional, so don't add one.

## Business rules (schema guide numbering; tests are named after them)

1. Current ownership shares on a property never exceed 100%. "Current" = ACTIVE + DISPUTED everywhere: a disputed
   share is still held (100% total, duplicate-owner check, `allocatedSharePct`, current owners, registers), it just
   cannot be transferred until the dispute is resolved.
2. History is never overwritten. A status change closes the old row (it starts on or before today and not before
   the current one), a new measurement or boundary survey supersedes the current one when it is the **latest dated**
   (same day or newer; an older survey is stored as history with `is_current = false` and a warning), lease terms are
   editable only while DRAFT (after that, `renew` creates a new row pointing back), a renewed rental ends the day
   before its renewal starts, and a building plan revision keeps the plan number with `revision_no + 1`.
3. An owner has at most one primary contact and one primary address. Setting a new primary clears the old one.
4. An agreement violation points at **exactly one** agreement (lease, rental or transfer), matching
   its `agreement_type`. The DB enforces this with a check using `num_nonnulls`.
5. The **3rd violation inside the notice period** cancels the lease or rental: `occurrence_no` counts the agreement's
   violations dated on or before this one, and the 3rd (or later) cancels only when it falls inside the period of a
   notice served on an earlier violation (`notice_date <= violation_date <= notice_deadline`). A violation carries one
   notice; a notice without a comply-by date is accepted with a warning (the rule cannot apply to it).
6. A departmental appeal's decision is due on the appeal date + 120 days. The response warns if the appeal
   was filed more than 30 days after the order was received.
7. Every area is also stored in square feet in `*_base` columns, converted with `measurement_unit.factor_to_base`.
   SQFT is the base unit.
8. Business codes (PROP-00001 …) come from `code_sequence`. Templates are editable (`PUT /code-sequences/{key}`),
   but a template can't move back onto a code already in use.
9. Workflows compare masters by **code**, never by id or name. Codes the code depends on are listed in
   `Domain/Models/MasterData/SystemMasterCodes.cs`. The seeder ensures they exist, and they can't be deactivated.
10. A document attached to a record (entity_type + entity_id) requires that record to exist on that property. An
    owner's paper belongs to the owner alone (no property), needs an OWNER document type and an active owner; all
    checks run before the file is written, so a refused upload leaves no file behind.
11. An owner is deactivated only when they hold no active or disputed share, are no party to a pending (initiated or
    approved) transfer and hold no allotment, lease (also a draft), rental or contract in force - records on
    deactivated properties don't count. The refusal lists what blocks it (`PropertyOwner.Deactivate(Commitments)`).

GDA Act rules:
- s.28-A: a fine is at most Rs 1,000,000.
- s.28-A fines and s.30 criminal complaints need an **authorized officer**, the Identity flag
  `is_authorized_officer`. It arrives as JWT claim `ao` and is read through `ICurrentUser.IsAuthorizedOfficer`.
  Without it the API returns 403 (`AuthorizedOfficerRequiredException`). A complaint is recorded against the
  signed-in officer.
- s.6(4)(c): an allotment can be cancelled and restored. Allotment is not ownership until `confirm-ownership`.

## Ownership, transfers, allotments, charges (the rules the register depends on)

All of them read one `OwnershipRecord` (Domain/Models/Ownership): every ownership row of the property (tracked),
the owners involved, the other waiting transfers, the ACTIVE charges, and today. Handlers build it with
`context.LoadOwnershipRecordAsync(property, partyIds)`; "today" is `EntityLoaders.Today` (UTC date).

- **Approving a transfer IS the mutation.** `POST /transfers/{id}/approve` re-checks every rule and, in the same save,
  closes the givers' rows and opens the receivers' rows (status COMPLETED, `completedOn` = today, `completedBy` = the
  signed-in user, optional `approvalOrderRef`). The only waiting state is INITIATED. `/complete` only registers rows still
  in the legacy APPROVED state. `/transfers/completed` = initiate + approve in one step. `/reject {reason}` (APPROVE
  permission) and `/cancel {reason}` (EDIT; a legacy APPROVED one also needs APPROVE, else 403) record `closedOn`,
  `closedBy`, `closingReason`. A completed transfer is reversed by a new transfer.
- **Transfer rules** (at initiate and again at approve): both sides balance; the transfer must move a share ("This
  transfer moves no share."); every party is an active owner; givers hold an ACTIVE share — a disputed giver is refused;
  a giver's share minus what other waiting transfers already give must cover this one (double commitment, the message
  names the other TRF); no ACTIVE charge on a giver's row or on the whole property; the transfer date is after every
  party's earlier entries on the property (one entry per owner per day, no overlapping periods). Initiate may be future
  dated; approve waits until the transfer date; approval date between the transfer date and today.
- **Rows after a transfer**: a net giver who keeps part gets a remainder row that keeps the closed row's acquisition
  details ("Remainder after TRF-…"); a net receiver's free rows are merged into one new row; a receiver's disputed or
  charged row is left as it is. `OwnershipDto.HoldingSince` walks back through the owner's rows that ended where the next
  began (`OwnershipHistory.HoldingSince`).
- **Add owner / end ownership**: effectiveFrom / effectiveTo not in the future; add refuses an owner who already holds a
  share and dates before the owner's earlier entries; end refuses a charged share (row or whole property) and a share being
  given in a waiting transfer.
- **Allotments**: only GDA_PROPERTY (`PropertyAllotment.GdaPropertyClassificationCode`) is allotted; at most one allotment in
  force (ACTIVE / RESTORED, active) per property, checked at allot and restore; allotment date not in the future. Status act:
  in force -> SURRENDERED (date, `surrenderDate`) or EXPIRED (after `expiryDate`; without one the given date becomes it);
  never back to ACTIVE. Cancel only from in force. Cancel / surrender end the ownership confirmed through the allotment
  (remark "Ended by cancellation of ALT-…"); restore does not reopen it. Confirm as owner: in force, not confirmed since the
  last restoration, effectiveFrom between the allotment (or restoration) date and today, refused when someone holds 100%.
- **Charges (encumbrances)**: only against a current ownership row; release date between start and today.
- **Deactivating a property** is refused while it has a waiting transfer, an allotment in force, an ACTIVE lease, rental
  or contract, or an open legal matter (OpenMatters); the message lists them. Every write in this area refuses a
  deactivated property.

## Land and legal lifecycle rules (domain methods; tests in `LandAndLegalTests.cs`)

- **Active property**: every write on a record hanging off a property (plans, regularizations, measurements, surveys,
  leases, rentals, contracts, auctions, violations, encroachments, cases, appeals) needs the property active.
  Handlers of actions on existing records call `context.EnsurePropertyActiveAsync(record.PropertyId)`
  (`Services/PropertyWriteGuard.cs`, which also has `Today()`).
- **Dates**: status start, measured / survey / detection dates, notice dates, violation date and award date cannot be
  in the future. Built-up area is at most the total area.
- **Regularization**: withdrawing (deactivate) twice is refused; `GET /properties/{id}/regularizations` lists withdrawn
  cases too by default (`isActive = false`).
- **Building plan**: APPROVED, REJECTED, REVISED and WITHDRAWN close a revision (`BuildingPlan.ClosedStatusCodes`).
- **Encroachment**: "in place since" (`effective_date`) is on or before the detection date; a notice date needs a
  notice no.; a notice moves ACTIVE → UNDER_NOTICE; a court case filed with `relatedEncroachmentId` moves an
  unresolved one to UNDER_LITIGATION.
- **Court cases**: `PropertyLitigation.ClosedStatusCodes` (DECIDED, APPEALED, WITHDRAWN, SETTLED, CLOSED) is the one
  closed set; a closed case takes no hearing, party, decision, edit or status change, and a new case cannot start
  closed. `filed_by_officer_id` is set only for a criminal complaint (the signed-in authorized officer) and never
  overwritten; an edit needs an officer only when it turns the case into a complaint. An appeal as a new case needs a
  DECIDED lower-court case.
- **Departmental appeals**: one order is appealed once (an open or decided appeal on the same record, or the same order
  ref, blocks another; a withdrawn one does not); the appealed record cannot change on update. On a violation:
  filing stores `pre_appeal_status`; ALLOWED sets it aside (RECTIFIED + remark "Set aside on appeal APL-…", imposed
  fine WAIVED) and reinstates a lease / rental it cancelled ("Reinstated by appeal APL-…"); any other outcome or a
  withdrawal restores `pre_appeal_status`. A violation that cancelled its agreement can never be rectified.
- **Auctions**: AWARDED, UNSUCCESSFUL and CANCELLED are closed (`PropertyAuction.ClosedStatusCodes`); SUCCESSFUL only
  through award; the award needs the auction date and is dated from it up to today.
  `POST /auctions/{id}/re-auction` plans a RE_AUCTION (once per auction) linked by `previous_auction_id`.
- **Leases / contracts**: "mark expired" only after the end date (a DRAFT lease cannot expire; an open-ended contract
  is terminated instead).

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
| Owners | `/owners` (search: code, CNIC or its first 5+ digits, NTN, name; `cnic` / `ntn` exact lookup; `hasHoldings`; `sortBy` code / name / type / holdings), contacts, addresses, documents | `PROPERTY.*` |
| Ownership | `/properties/{id}/ownerships`, `/transfers` (initiate), `/transfers/completed` (one step), `/transfers/{id}/approve|reject|complete|cancel`, `/encumbrances/*` | approve, reject, complete and the one-step record need `PROPERTY.APPROVE`; cancelling a legacy APPROVED transfer too |
| Management | `/properties/{id}/allotments|leases|rentals|violations|auctions|outsourcing-contracts` + action routes (`/leases/{id}/renew`, `/violations/{id}/fine` …) | official orders need `PROPERTY.APPROVE` |
| Compliance | `/properties/{id}/boundaries|encroachments|litigations|appeals|building-plans` + action routes | official orders need `PROPERTY.APPROVE` |
| Documents | `/properties/{id}/documents` (PROPERTY kinds), `/owners/{id}/documents` (OWNER kinds, no `propertyId`), `/documents/{id}` (details, re-type within scope), `/documents/{id}/versions|content|activate|deactivate` | `PROPERTY.*` |
| Cross-property lists | `GET /agreements`, `/legal-matters`, `/transfers` (pageSize 1-5000) | `PROPERTY.VIEW` |
| Reports | `GET /property-reports/register` (same filters as `/properties`, max 5000, or picked `ids`), `/property-reports/ownership?asOf=` | `PROPERTY.VIEW` |

Errors are ProblemDetails, mapped in `PropertyExceptionHandler.cs`:
- 400: ValidationException (with `ValidationErrors`), DomainException, bad request
- 403: AuthorizedOfficerRequiredException, PermissionRequiredException (a permission decided by the record, e.g. cancelling an approved transfer)
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
  **In force** (allotment ACTIVE / RESTORED, lease ACTIVE, rental and contract ACTIVE, auction PLANNED / ANNOUNCED /
  CONDUCTED) lives in `Services/InForce.cs`. A **current share** is ACTIVE or DISPUTED
  (`PropertyQueryFilter.CurrentHoldings`).
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
- Owner papers have no `propertyId`; `GET /properties/{id}/documents` never lists them. Sending `propertyId` on an
  owner upload is refused.
- Master values added after the first release (the owner document kinds) are in `MasterDataSeed.AddedLater`: the
  seeder adds each by code where missing, also to existing databases, and leaves existing rows alone.
- Transfers move shares: the transferor side and the transferee side must balance. A gift needs `relationship`.
  Approving a transfer registers it (there is no separate "complete" step for new transfers). `/transfers/completed`
  records a past transfer as COMPLETED in one step, using the same rules.
- An appeal against a fine marks the violation APPEALED. A decided appeal is final; its decision (or a withdrawal)
  moves the violation out of APPEALED (see "Land and legal lifecycle rules").
- `DomainException` wraps its message (`Domain Exception "…" throws from Domain layer`); domain tests assert with
  `Assert.Contains`.
- The cross-property lists merge several tables in memory after SQL filtering. Keep paging through
  `ListSorting.Page` so ordering stays stable.
- Don't add CHECK constraints, columns or tables that aren't in the DBML. Extra rules belong in the domain.
