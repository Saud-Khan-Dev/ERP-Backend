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
src/Tests/PropertyManagement.Domain.Tests/   xUnit tests of domain rules (287), in ERP.slnx under /Tests/
```

## Domain model (DBML tables → classes)

- **Property core**: `Property` (PROP-00001), `PropertyStatusHistory`, `PropertyMeasurement`
  (current + superseded), `PropertyAreaRegularization`.
- **Owners**: `PropertyOwner` (OWN-00001; person or organization), `OwnerContact`, `OwnerAddress`. CNIC: 13 digits
  stored 12345-1234567-1, never starting with 0 (`Cnic`). NTN: FBR 7+1 stored 1234567-8, or a 13-digit CNIC-based
  number stored 12345-1234567-1 (`Ntn`); duplicates are compared on the digits. By owner type (system codes):
  INDIVIDUAL needs a CNIC; COMPANY / TRUST / GOVERNMENT / JOINT_ENTITY have no CNIC and no father / husband name.
  Old records (2026-10-26): an individual who never had a CNIC is entered with `oldIdType` OLD_NIC / PASSPORT (+ `oldIdNo`)
  or NOT_RECORDED instead, and may be `isDeceased` (`dateOfDeath` not in the future). `CanHoldToday`: a share registered
  today (first owners, a transfer's receiver) needs a CNIC unless the holder has died - people known only by an old ID
  appear in a property's history (below); no share passes to someone after their known date of death.
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
   cannot be transferred until the dispute is resolved. An owner holds at most ONE current row per property (partial
   unique index `ux_property_ownership_current_holding` on (property_id, owner_id) where ACTIVE / DISPUTED).
   **Owners come in only through a transfer once a property is held** (2026-10-26): owners are entered directly only
   while nobody holds it, all at once with exactly 100% between them (see "First owners" below).
2. History is never overwritten. A status change closes the old row (it starts on or before today and not before
   the current one), a new measurement or boundary survey supersedes the current one when it is the **latest dated**
   (same day or newer; an older survey is stored as history with `is_current = false` and a warning), lease terms are
   editable only while DRAFT (after that, `renew` creates a new row pointing back), a renewed rental ends the day
   before its renewal starts, and a building plan revision keeps the plan number with `revision_no + 1`.
3. An owner has at most one primary contact and one primary address. Setting a new primary clears the old one.
4. An agreement violation points at **exactly one** agreement (lease, rental, transfer or allotment - type ALLOTMENT,
   a breach of allotment conditions), matching its `agreement_type`; the violator must be that agreement's party
   (lessee / tenant / a transferee / allottee). The DB enforces one agreement with a check using `num_nonnulls`.
5. The **3rd violation inside the notice period** cancels the lease, rental or allotment (recording such a violation
   needs `PROPERTY.APPROVE`, else 403): `occurrence_no` counts the agreement's
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
- s.28-A: a fine is at most Rs 1,000,000. Fine status (`POST /violations/{id}/fine-status`): PAID needs receipt no. +
  paid-on date (not future, not before imposed); WAIVED needs a reason and PROPERTY.WAIVE; RECOVERY_AS_ARREARS needs PROPERTY.WAIVE; PAID needs EDIT;
  PAID and WAIVED are final. Who / when is stored (`fine_status_changed_by/_on`).
- s.28-A fines and s.30 criminal complaints need an **authorized officer**, the Identity flag
  `is_authorized_officer`. It arrives as JWT claim `ao` and is read through `ICurrentUser.IsAuthorizedOfficer`.
  Without it the API returns 403 (`AuthorizedOfficerRequiredException`). A complaint is recorded against the
  signed-in officer.
- s.6(4)(c): an allotment can be cancelled and restored. Allotment is not ownership until `confirm-ownership`.

## Ownership, transfers, allotments, charges (the rules the register depends on)

All of them read one `OwnershipRecord` (Domain/Models/Ownership): every ownership row of the property (tracked),
the owners involved, the other waiting transfers, the ACTIVE and ENFORCED charges, the allotment in force, and today.
Handlers build it with `context.LoadOwnershipRecordAsync(property, partyIds)`; "today" is `PakistanDate.Today`
(Asia/Karachi). Loading the record **touches the property row** (`IApplicationDbContext.Touch`), so two acts on one
property at the same moment clash on its `xmin` row version and the second gets 409 ("Someone else changed this
property's records at the same moment…"). `property`, `property_transfer` and `property_allotment` carry `xmin`.

- **Approving a transfer IS the mutation.** `POST /transfers/{id}/approve` re-checks every rule and, in the same save,
  closes the givers' rows and opens the receivers' rows (status COMPLETED, `completedOn` = today, `completedBy` = the
  signed-in user, optional `approvalOrderRef`). The approver is the **signed-in officer** (`approved_by_user_id`,
  `approved_by_employee_id` when linked) - the request carries no "approved by". Maker-checker is the
  **PROPERTY_TRANSFER separation-of-duties rule** (BuildingBlock `DutyRules`, on unless a Super Admin switched it off in
  Administration; `GET /property-settings` `requireSeparateApprover` shows its state read-only): the person who started or
  recorded a transfer is refused ("You cannot approve TRF-…, which you started: transfers pass through two people …"),
  checked in the handler before anything changes. `/transfers/completed` while the rule is on saves the past transfer
  INITIATED (`awaitingApproval`, `recordedPast` on the transfer) with the approval date / order / no-dues certificate read
  off the paper kept on it (`PropertyTransfer.RecordForApproval`); a second officer approves it (approve defaults to those
  values). With the rule off it is one step as before. `noDuesCertificateNo` (Taxation clearance, checked and consumed by
  the UI through Taxation's API - a missing one is only a warning) is stored. A transfer dated more than 90 days ahead
  is refused unless `confirmFarFuture`. The only waiting state is INITIATED. `/complete` only registers rows still
  in the legacy APPROVED state. `/transfers/completed` = initiate + approve in one step (rule off) or initiate for a second officer (rule on).
  `/reject {reason}` (APPROVE permission) and `/cancel {reason}` (PROPERTY_LEGAL.EDIT; a legacy APPROVED one also needs
  APPROVE, else 403) record `closedOn`,
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
- **First owners** (`POST /properties/{id}/ownerships` `{ owners: [...] }`, `PropertyOwnership.RegisterFirstOwners`; also
  the `owners` of `POST /properties`): only while the property has NO current owner (no ACTIVE or DISPUTED row) - a held
  property is refused (400 "PROP-x is already registered in the name of A (60%) and B (40%). A new owner can only come in
  through a transfer: use "Start transfer", or "Record a completed transfer" …"). Several owners in one request (joint
  owners), each once, shares adding up to EXACTLY 100% (a partial first entry is refused: the rest could never be added
  later), effectiveFrom not in the future and after each owner's own earlier entries on the property; refused while an
  allotment is in force (confirm the allottee instead). PROPERTY.EDIT on a property nobody has held yet (entered from the
  register); a property that was held before (its shares ended through an allotment's cancellation / surrender, or by
  hand before 2026-10-26) needs PROPERTY.APPROVE (403). Every `POST /properties` with owners obeys the same 100% rule
  (the data had no partial totals when the rule came in).
- **No "end ownership"**: the route `POST /ownerships/{id}/end` and its command were removed on 2026-10-26 (404 now). A
  share ends only when it is transferred (approve / record a completed transfer) or when the allotment it was confirmed
  through is cancelled or surrendered. Rows ended by hand earlier keep their `end_reference_no`. Consequence: a property
  somebody holds cannot be deactivated ("PROP-x is held by A (100%), so it stays on the register: a share held ends only
  when it is transferred."), and neither can an owner holding a share of an active property.
- **Allotments** (allot, cancel, restore, surrender, confirm: APPROVE): only GDA_PROPERTY
  (`PropertyAllotment.GdaPropertyClassificationCode`) is allotted, and only land no private holder has a share in (a
  GOVERNMENT owner's share is fine; "PROP-x is held by A (61%)…; only land GDA holds can be allotted"). `TakesEffectOn`
  (allotment or restoration date) may be in the future: such an allotment is current (`isCurrent`) but not in force
  (`isInForce`) until then, and confirm waits for it. Classification cannot change while an allotment is in force; at most one allotment in
  force (ACTIVE / RESTORED, active) per property, checked at allot and restore; allotment date not in the future. Status act:
  in force -> SURRENDERED (date, `surrenderDate`) or EXPIRED (after `expiryDate`; without one the given date becomes it);
  never back to ACTIVE. Cancel only from in force. Cancel / surrender end the ownership confirmed through the allotment
  (remark "Ended by cancellation of ALT-…"); restore does not reopen it. Confirm as owner: in force, not confirmed since the
  last restoration, effectiveFrom between the allotment (or restoration) date and today, refused when someone holds 100%.
- **Ownership history (old records, 2026-10-26)**: `OwnershipHistoryEntry` (HIS-00001, `ownership_history_entry`, steps
  as jsonb). A clerk (EDIT) enters a property's ownership from 1960 onwards in one go - step 1 the first owner(s) on record
  (100%, how acquired e.g. FIRST_REGISTRATION), then each transfer oldest first - with each date's precision (EXACT,
  MONTH, YEAR, ABOUT; a month / year is stored as its first day). It is played forward at once in a sandbox through
  `RegisterFirstOwners` + `PropertyTransfer.RecordCompleted` (every transfer rule; `OwnershipRecord.ReplayingHistory`
  lets old-ID owners receive shares on the way) and waits (WAITING); a second officer (APPROVE, PROPERTY_TRANSFER duty
  rule) registers it ONCE: it is replayed against the property as it stands, COMPLETED transfers get new TRF numbers
  (precision kept on transfers and the rows they open), papers filed per step (`POST /ownership-history/{id}/steps/{n}/documents`,
  entity type OWNERSHIP_HISTORY) move to the step's transfer / first holding. With the rule off, an APPROVE holder's entry
  registers at once. Fits only a property with no history in the register (no ended rows, present rows not acquired via
  a transfer or allotment, none waiting): nobody holds it (the last holders must be able to hold today), or the history
  must end with EXACTLY the present holders (owner, share, start date) - their rows then adopt how they were acquired
  (`AdoptHistoryOf`). Revise (EDIT, WAITING), reject (APPROVE) / withdraw (EDIT) with a reason. `DomainException.Reason`
  is the bare rule sentence (a refusal is re-worded "Step 3 (about 1975): ...").
- **Corrections (APPROVE, reason required, audit "Corrected")**: `POST /transfers/{id}/corrections` (COMPLETED: kind, date +
  precision, refs, price, relationship, approval order, remarks; a new date moves the ends of the rows it closed and the
  starts of those it opened - after each closed row began, before each opened row passed on, one entry per owner per day);
  `POST /ownerships/{id}/corrections` (acquisition kind, ref, remarks; start date + precision only for a row no transfer /
  allotment opened). `PUT /properties/{id}/original-registration` (EDIT): `registeredOn` + precision + reference, not
  after the first holding. `GET /properties/{id}/ownership-timeline` (VIEW): the story for the screens - owners numbered
  in the order they came to hold it (identity incl. old ID / deceased), events (FirstOwners / Transfer / Allotment) with
  papers (latest version, `MaySee`), present holdings, `canEnterHistory`, `waitingHistoryId`. Bell key `history.waiting`
  (APPROVE). The 14 "OTHER" master values were switched off by migration OwnershipHistoryRecords (no catch-all rule);
  transfer types FIRST_REGISTRATION and COURT_DECREE are added where missing (`MasterDataSeed.AddedLater`).
- **Charges (encumbrances)**: only against a current ownership row; start date not in the future; release date between
  start and today. Release / enforce need APPROVE; enforce needs `holderOwnerId`. An ENFORCED charge still blocks the share
  except a transfer to the charge holder, and that transfer, once registered, releases it (`releaseReferenceNo` = TRF no.).
- **Deactivating a property** needs a reason (`deactivated_on`, `deactivation_reason`) and is refused while somebody holds
  it (the message names the holders: a share ends only through a transfer, so a held property stays on the register), and
  while it has a waiting transfer, a charge, an allotment in force, an ACTIVE or DRAFT lease, rental or contract, a
  running auction, a plan under consideration, or an open legal matter (OpenMatters); the message lists them. Every write in this area refuses a
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
- **Leases / rentals / contracts** (`AgreementSpan`, `AgreementOverlap`, `AgreementDates`): DRAFT or ACTIVE on create
  (`asDraft`), `activate`; terms change only while DRAFT, once ACTIVE the PUT takes only reference / date / remarks (a
  contract also `moneyDirection` once); `renew` (rental from ACTIVE or EXPIRED; contract -> old RENEWED, ends the day
  before); `expire` only after the end date (open-ended -> terminate); `terminate` (APPROVE) -> TERMINATED, dated not in
  the future, not after the agreed end, not before the start - an agreement not started yet is called off today;
  `withdraw {reason}` a draft -> WITHDRAWN. One ACTIVE lease / rental at a time (contracts: one per type); other kinds
  running give `warnings[]`. Contract `moneyDirection` RECEIVABLE / PAYABLE is required on create. Rental
  `CurrentRent(asOf)` = rent x (1 + escalation)^full years. Auctions are never "in force".
- **Encroachment resolve, court decision, appeal decision, plan approve/reject, regularization Regularized/Rejected**:
  APPROVE; their dates are never in the future. A decision needs `decisionResult` (IN_FAVOUR_OF_GDA / AGAINST_GDA /
  PARTLY_IN_FAVOUR) and may resolve the related encroachment (`effects[]`). An appeal ALLOWED against an allotment
  cancellation restores it, against a plan rejection reopens the plan. A decided regularization is final (only
  effective-to, order ref, remarks change). Plan approval warns on a steep plot (> 45 degrees). Staff named on a record
  (hearing attended by, GDA counsel, delegated officer, plan / regularization approver) carry an HRM employee id plus the
  name; outsiders are a name only.

## Dynamic master data

`Application/MasterData/MasterRegistry.cs` lists every master with its slug. One generic API serves all of them:
`GET/POST /masters/{slug}`, `PUT /masters/{slug}/{id}`, `POST …/{id}/activate|deactivate`, `GET /masters`.
**Adding a master** takes a class in `Masters.cs`, one line in `MasterRegistry.All`, EF config in
`MasterDataConfiguration.cs`, seed rows in `MasterDataSeed.cs`, and a migration. No endpoints are needed. Code and name
lengths per table come from `MasterLimits`. Seeded values follow the schema guide. Since Saad's change there is
no "Other" value in any master list. `GET /masters` marks `readOnly` lists (allotment / lease / rental / contract
statuses, agreement types: rename only); items carry `isSystem` (a `SystemMasterCodes` value: cannot be switched off or
deleted) and `usageCount` (generic FK count, `ReferenceCountsAsync`). `DELETE /masters/{slug}/{id}` (PROPERTY_SETUP.DELETE)
only at usage 0. A measurement unit in use keeps its factor.

Custom fields: the code is optional (made from the label, `AttributeCodes.FromLabel`); `appliesToPropertyTypeIds` /
`appliesToClassificationIds` (uuid[], empty = all) scope a field; `GET /attribute-definitions?propertyTypeId=&classificationId=`
returns what a registration of such a property shows. Once a field holds values its data type and the options in use
are locked; `DELETE /attribute-definitions/{id}` only when no property holds a value. `GET /properties/{id}/attributes`
returns applicable fields, then `isHidden` read-only values of switched-off / not-applying fields.

Audit trail (since 2026-10-22): the shared `AuditTrailInterceptor` writes `audit_entry` (append-only, triggers) for every
record added, changed (one line per field, old -> new, names not ids; renames of the complex `PropertyName` / `OwnerName`
included) or removed, with who (display name), when, address, device and request id. `Application/Services/PropertyAudit.cs`
is the profile: kinds in words, codes, and the parent - every record on a property hangs off the PROPERTY (case parties,
hearings, bids and transfer parties through their back-navigations `Litigation` / `Auction` / `Transfer`), an owner's
contacts, addresses and own papers off the OWNER; boundary points are skipped (the survey line keeps the facts; capturing
an encroached polygon writes one "Encroached area boundary" line). Decisions add a verb line (`PropertyAuditActions`:
Approved, Rejected, Cancelled, Recorded for approval, Allotted, Restored, Confirmed, Terminated, Released, Enforced,
Decided, Resolved, Awarded, Imposed, Waived, Sent for recovery, Activated, Deactivated; older "Ended" lines are from the
removed "end ownership" act) with the reason / order; opening a
paper writes "Downloaded". Handlers get the same DbContext instance the trail writes to (`IApplicationDbContext` resolves
`ApplicationDbContext`). `GET /properties/{id}/history` and `/owners/{id}/history` (PROPERTY.VIEW; `before`, `from`, `to`,
`action`, `search`, `pageSize`) answer `{ audit: { data, count }, restrictedCount }`; without PROPERTY_LEGAL.VIEW legal
kinds and hidden papers are left out. `GET /audit`, `/audit/kinds` (IAM_AUDIT.VIEW), `POST /audit/exports` (PROPERTY.EXPORT,
the screens' CSV notes) and `GET /system/status` (IAM_SYSTEM.VIEW) come from the building block. The old `property_change`
table is read-only (its 4,800 lines were copied into `audit_entry` by migration `AuditTrail`).

Town scope (`ICurrentUser.TownScope`, set in Administration): named EF query filters (`ApplicationDbContext.TownScopeFilter`)
narrow `Property` and every record on a property (and their parts) to the person's towns, so lists, counts, reports, the
bell and reads (404) follow it; writes on such records 404. Registering or moving a property to a town outside the scope is
403. Owners are not filtered by the model (a party's name on a record in scope always shows): the owner screens use
`OwnerScope` - visible when they hold or held a share in scope, are party to an allotment / lease / rental / contract there,
or hold no share anywhere (`OwnershipLink`, a view over `property_ownership` no filter narrows). `GET /owners?cnic=` / `?ntn=`
(exact lookups) search every town so a person holding land elsewhere is used, not registered twice. Checks that must see
every town call `IgnoreQueryFilters()` (codes in use, case numbers, an owner's commitments, custom-field values).
`GET /directory/towns` (signed in): every active town `{ towns: [{ id, code, name }] }` for Administration's scope picker.

Bell: `GET /attention` (any signed-in user; counts follow the caller's rights and towns): transfers.pending,
transfers.unregistered, history.waiting, plans.waiting, allotments.unconfirmed (APPROVE); appeals.overdue (LEGAL.APPROVE); agreements.ending
(60 days), agreements.overdue (EDIT); hearings.week, legal.next-passed, encroachments.open (LEGAL.EDIT); fines.unpaid (EDIT
or WAIVE). The frontend maps them in
`features/property/property.attention.ts`.

## API map (permissions from `PermissionCatalog`)

| Area | Routes | Permission |
| --- | --- | --- |
| Masters, code sequences, custom-field definitions | `/masters/*`, `/code-sequences`, `/attribute-definitions` | `PROPERTY_SETUP.*`; reads of masters and definitions also accept `PROPERTY.VIEW`; deletes need `PROPERTY_SETUP.DELETE` |
| Settings, bell, history | `GET /property-settings`, `GET /attention` (signed in), `GET /properties/{id}/history`, `GET /owners/{id}/history`, `GET /directory/towns` (signed in) | `PROPERTY.VIEW` (settings also `PROPERTY_SETUP.VIEW`) |
| Audit trail, system | `GET /audit`, `/audit/kinds`; `POST /audit/exports`; `GET /system/status` | `IAM_AUDIT.VIEW`; `PROPERTY.EXPORT`; `IAM_SYSTEM.VIEW` |
| Legal matters | encroachments, court cases (parties, hearings), appeals: reads, `/litigations/upcoming-hearings`, `/appeals/overdue`; confidential papers and papers on those records; `/legal-matters` kinds encroachment / case / appeal (violations only without it) | `PROPERTY_LEGAL.VIEW` |
| Legal work | record / edit / notice / status / boundary of encroachments; file / edit / party / hearing / status of cases; file / edit / start hearing / withdraw appeals; violation notice; ownership dispute mark / resolve; auction bids; transfer cancel; papers filed on legal records | `PROPERTY_LEGAL.EDIT` |
| Legal decisions | resolve an encroachment, decide a case or appeal, award an auction | `PROPERTY_LEGAL.APPROVE` |
| Fines | fine-status WAIVED / RECOVERY_AS_ARREARS (PAID needs EDIT; the route accepts either) | `PROPERTY.WAIVE` |
| Register | `GET /properties` (filters incl. transferPending, allotted, agreementInForce, hasHolder, charged, disputed, measured, hasBoundary; sort incl. holder; paging 1-200), `GET /properties/next-code`, `POST /properties` (can also save owners, boundary and custom fields in one transaction), `/properties/{id}/status`, `/measurements`, `/regularizations`, `/area`, `/attributes` | `PROPERTY.VIEW/CREATE/EDIT` |
| Owners | `/owners` (search: code, CNIC or its first 5+ digits, NTN, name; `cnic` / `ntn` exact lookup; `hasHoldings`; `role` holder / allottee / lessee / tenant / contractor / bidder / encroacher / litigant / appellant / violator; `sortBy` code / name / type / holdings), contacts, addresses, documents | `PROPERTY.*` |
| Ownership | `/properties/{id}/ownership-timeline`, `/properties/{id}/ownership-history` (+ `/ownership-history/{id}` revise / steps/{n}/documents / register / reject / withdraw), `/transfers/{id}/corrections`, `/ownerships/{id}/corrections`, `/properties/{id}/original-registration`, `/properties/{id}/ownerships` (GET; POST = first owners, only while nobody holds the property), `/transfers` (initiate), `/transfers/completed` (past transfer), `/transfers/{id}/approve|reject|complete|cancel`, `/encumbrances/*` | first owners `PROPERTY.EDIT` (+ APPROVE on a property held before); approve, reject, complete and the past-transfer record need `PROPERTY.APPROVE`; cancel needs `PROPERTY_LEGAL.EDIT` (+ APPROVE for a legacy APPROVED one); disputes `PROPERTY_LEGAL.EDIT` |
| Management | `/properties/{id}/allotments|leases|rentals|violations|auctions|outsourcing-contracts` + action routes (`/leases/{id}/renew`, `/violations/{id}/fine` …) | official orders need `PROPERTY.APPROVE` |
| Compliance | `/properties/{id}/boundaries|encroachments|litigations|appeals|building-plans` + action routes | official orders need `PROPERTY.APPROVE` |
| Documents | `/properties/{id}/documents` (PROPERTY kinds), `/owners/{id}/documents` (OWNER kinds, no `propertyId`), `/documents/{id}` (details, re-type within scope), `/documents/{id}/versions|content|activate|deactivate` | `PROPERTY.*`; an owner upload is `PROPERTY.EDIT`, or `PROPERTY.CREATE` for an owner's FIRST CNIC copy only (other kinds, or a second copy, 403) - who registers owners files the copy with the registration |
| Cross-property lists | `GET /agreements` (+ `direction`, `unconfirmed`), `/legal-matters` (+ `fineUnpaid`), `/transfers`, `/building-plans` (`awaitingDecision`, latest revision per plan) (pageSize 1-5000) | `PROPERTY.VIEW` |
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
4. `PropertyRulesOverhaul` (2026-10-13)
5. `PropertyAuditFixes` (2026-10-20): approver / employee ids, no-dues certificate nos., end reference, deactivation
   reason, contract money direction and renewal link, case result and counsel, fine payment columns, violation on an
   allotment (check widened), xmin row versions (property, transfer, allotment - system column, nothing to create).
6. `PropertyScopesAndChangeLog` (2026-10-20): custom-field scope arrays, `property_change`.
7. `AuditTrail` (2026-10-22): `audit_entry` with append-only triggers, the old change log copied into it. (The snapshot's
   `OwnershipLink` view line is `b.ToTable((string)null)`: EF writes `ToTable(null, "property")`, which throws.)
8. `OwnershipOnlyThroughTransfers` (2026-10-26): partial unique index `ux_property_ownership_current_holding` on
   `property_ownership (property_id, owner_id) WHERE ownership_status IN ('ACTIVE','DISPUTED')`. It stops with directions
   if an owner already holds two current rows of one property. (Hand-fixed `ToTable((string)null)` again in the designer
   and snapshot.) A transfer that leaves a seller a remainder or merges a receiver's rows closes the old row and opens the
   new one in one save; EF orders the UPDATE before the INSERT, so the index holds (checked on a copy of the data).

Create migrations from `src/` with
`dotnet ef migrations add <Name> -p Services/PropertyManagement/PropertyManagement.Infrastructure -s Services/PropertyManagement/PropertyManagement.Api`.
EF can't index a complex property, so `ix_property_owner_owner_name` is created with SQL inside a migration.

Config (`appsettings.json`):
- `ConnectionStrings:Database`
- `Jwt:Issuer/Audience/SigningKey`: the key must equal Identity's
- `FileStorage:RootPath`: documents folder; compose mounts the `property-files` volume
- `Documents:MaxFileSizeBytes` (25 MB) and `Documents:AllowedExtensions`
- (`Transfers:RequireSeparateApprover` is gone: the PROPERTY_TRANSFER duty rule in Administration replaced it.)

## Testing

- `dotnet test src/ERP.slnx` runs the domain tests (the 2026-10-20 audit rules: `AuditFixRuleTests.cs`; API flows: `ERP-Frontend/tools/e2e/property-api.e2e.mjs`). Add a test for every new domain rule, named after the rule or
  Act section. Use `Fixture.cs` helpers (`Master<T>`, `Unit`, `Property`, `Owner`, `D("2026-01-01")`).
- For API behavior, run Identity + Property against Postgres, then step through `PropertyManagement.Api.http`.

## Gotchas

- A change to a user's authorized-officer flag or roles takes effect only with their **next access token**.
- Owner papers have no `propertyId`; `GET /properties/{id}/documents` never lists them. Sending `propertyId` on an
  owner upload is refused.
- Master values added after the first release (the owner document kinds) are in `MasterDataSeed.AddedLater`: the
  seeder adds each by code where missing, also to existing databases, and leaves existing rows alone.
- Transfers move shares: the transferor side and the transferee side must balance. A gift needs `relationship`.
  Once a property is held a transfer is the ONLY way a new owner comes in, and the only way a share ends (besides an
  allotment's cancellation / surrender) - there is no "end ownership" act and no "add owner" on a held property.
  Approving a transfer registers it (there is no separate "complete" step for new transfers). `/transfers/completed`
  records a past transfer with the same rules: COMPLETED in one step only while the PROPERTY_TRANSFER rule is off, else it waits for a second officer.
- An appeal against a fine marks the violation APPEALED. A decided appeal is final; its decision (or a withdrawal)
  moves the violation out of APPEALED (see "Land and legal lifecycle rules").
- `DomainException` wraps its message (`Domain Exception "…" throws from Domain layer`); domain tests assert with
  `Assert.Contains`.
- The cross-property lists merge several tables in memory after SQL filtering. Keep paging through
  `ListSorting.Page` so ordering stays stable.
- Don't add CHECK constraints, columns or tables that aren't in the DBML. Extra rules belong in the domain.
- New DTO fields are appended with defaults so positional constructors keep compiling. A `DateOnly` default must be
  nullable (`DateOnly? X = null`): a `default(DateOnly)` breaks the OpenAPI document ("could not be converted to DateOnly").
- Unexpected errors return a plain sentence with a trace reference; never leak exception text to the user.
