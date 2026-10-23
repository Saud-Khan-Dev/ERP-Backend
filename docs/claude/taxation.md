# Taxation service: context for AI assistants

Read [overview.md](overview.md) first for the stack and shared conventions. This file covers `src/Services/Taxation` only.

## What it is

GDA's income module: the annual property tax demand and everything around it.

- tax files (one per taxable property), built from Property's data;
- the rate masters set by the Board: circles, heads, rate sets (locality factors and head rates), settings, special
  policies, penalty rules;
- demand runs and demands, with earlier years' arrears carried by year and head;
- notices in the Section's printed format (letterhead, table of particulars, QR / 1Link block), reviewed and confirmed
  before issue;
- the in-year and year-end surcharges;
- payments against each property's constant 1Link reference, reference syncs, SMS;
- case-by-case fees and clearance certificates.

- Port `http://localhost:5146` (launchSettings) / `5146:8080` in compose (`taxation-api`), gateway prefix `/taxation-service/`.
- Database `ERP_DB`, schema **`taxation`**, snake_case, history in `taxation.__EFMigrationsHistory`.
- OpenAPI at `/openapi/v1.json`. Every route needs a token. `Taxation.Api.http` walks through the main flow.

**Schema source: [`docs/gda_taxation_module.dbml`](../gda_taxation_module.dbml).** Table and column names, sizes and keys
match it. The rules come from the Tax Dummy prototype (`GDA/Tax Dummy prototype/js/engine.js`, outside this repo), which
the domain ports one for one, and from the recordings of 12 Oct 2026 the DBML cites.

What the DBML has and this service does not build yet (no rules decided, or no system to talk to): `challan` (one-off
PSIDs), `refund`, `sealing`, `water_connection`, `non_utilization_case`, `enlistment_licence`, `malba_security`,
`agreement_schedule_line`, `integration_inbox` / `integration_outbox`, `sync_checkpoint` and `import_batch` /
`import_row`. (`audit_log` is built; `job_run` is the service's own addition.) The columns that point at them (`payment.challan_id`, `demand_line.schedule_line_id`,
`arrears.import_row_id`) are plain uuids for now. `notice.sms_message_id` has no foreign key on purpose
(`sms_message.notice_id` points back; both are written in one save).

## Permissions, scope and separation of duties

| Module | Codes | Used for |
| --- | --- | --- |
| `TAX` | VIEW, CREATE, EDIT, DELETE, APPROVE, POST, EXPORT, RUN, REVERSE, WAIVE, ISSUE, ADJUST | CREATE opens files (a mapped category, not exempt, no uplift), computes runs, prepares notices, charges fees, registers parties; EDIT syncs files, keeps the details, reviews, confirms, serves notices, issues a hand-made notice at once, keeps parties; APPROVE approves / cancels demand runs and the in-year surcharge, sets overrides, holds, category and status, opens a file in another category / exempt / with an uplift, sets amounts and Normal/Special on a hand-made notice, issues pending notices in bulk; POST receives payments and takes fee receipts; DELETE removes a party with no fee and an opening-arrears row not yet billed; **EXPORT** notes a CSV / print in the trail (`POST /audit/exports`); **RUN** runs the daily job, the year-end surcharge and "prepare the notices due"; **REVERSE** reverses a payment; **WAIVE** waives a fee or charges one exempt; **ISSUE** issues and revokes clearances (and marks one used, as PROPERTY.APPROVE does); **ADJUST** adds and corrects opening arrears |
| `TAX_SETUP` | VIEW, CREATE, EDIT, DELETE, APPROVE | circles, heads, category map, rate sets (APPROVE approves one), settings (EDIT also takes back a future value), special policies, penalty rules (versions; EDIT takes back a future one), templates, letterhead, notice table. DELETE removes a circle, head or policy nothing refers to |

Setup lists, reports and the overview are readable with `TAX.VIEW` or `TAX_SETUP.VIEW` (`TaxAuthorization.AnyTaxReader`);
`GET /attention` needs only a signed-in token and lists what the caller may act on. **Calls that read Property (open and
sync a file, assign a town, map a property type) pass the caller's token on, so the caller also needs `PROPERTY.VIEW`.**
Holidays and post holders are read from HRM the same way; with no caller (the scheduled daily job) holidays come from
HRM's public `GET /directory/holidays?from&to` (`{ holidays: [{ date, name }] }`; a missing list counts as "could not be
read", so the job reports the weekends-only fallback).

**Town scope** (`ICurrentUser.TownScope`, `IApplicationDbContext.TownScope`, `Services/TaxScope.cs`): a person limited to
some towns sees and acts only on tax files of those towns and on everything under them (demands, notices, surcharges,
arrears, overrides, holds, payments, fees, clearances, reference syncs, SMS) - lists, reports, the overview and the bell
are filtered; a record outside is 404 (the loaders in `EntityLoaders` check it). Fees and receipts of a registered party
(no file) are seen by all. Opening a file for a property outside is "not found". Runs: a demand run / surcharge run needs
every town of its circle (all circles: no scope); the daily job, the year-end surcharge and "prepare the notices due" need
every town. Runs of a circle with none of the person's towns are hidden.

**Duty rules** (`IDutyRules`, Identity's switches, ON unless a Super Admin switched one off), checked before anything
changes: `TAX_DEMAND_RUN` (the run's `computed_by` does not approve it), `TAX_RATE_SET` (nobody who drafted any part of the
set - its `created_by`/`updated_by`, its factors' and rates' `created_by`, any trail line under it - approves it),
`TAX_PAYMENT_REVERSE` (the payment's `created_by`, else `received_by`, does not reverse it), `TAX_FEE_WAIVE` (the fee's
`created_by` does not waive it; charging a fee exempt in one go is refused while the rule is on). The makers are the rows'
own `created_by` (stamped from the token at insert, never changed), so no new column was needed.

## Layout

```text
Taxation.Domain/
  Abstraction/  Entity<TId>, Aggregate<TId>, ITypedId   Exceptions/  DomainException
  Common/       Guard, FiscalYear (2026 = FY 2026-27), Rupees (whole rupees, half away from zero), EnumText, DateRange
  Enums/        TaxEnums.cs: every value list of the DBML (stored UPPER_SNAKE)
  ValueObjects/ one typed id per table
  Models/Setup      TaxCircle (+ towns, staff), TaxHead (+ HeadCodes), CategoryMap, RateSet (+ LtfFactor, RateBand),
                    TaxSetting (+ SettingKeys, TaxSettings), SpecialPolicy, PenaltyRule, MessageTemplate,
                    NoticeTemplate, NoticeLetterhead, NoticeTableLayout (+ rows, row sources, columns)
  Models/TaxFiles   TaxFile (+ basis, payers, head values, overrides, holds, agreements and step revisions)
  Models/Demands    Demand (+ DemandLine, Arrears, Penalty), DemandRun, PenaltyRun, SurchargeRules
  Models/Notices    Notice (+ NoticeVariables, NoticeParticulars, NoticeChanges)
  Models/Payments   PsidSync, Payment (+ PaymentAllocation), Party, Fee, Clearance, SmsMessage
  Models/Jobs       JobRun (the daily job's runs)       Models/Audit  AuditLog
  Services/         TaxCalculator (the annual computation), TaxFormula (safe formula parser), Numbers, PaymentQr,
                    WorkCalendar, NoticeTable (the table of particulars), NoticeServices (NoticeLadder, FilePrefix)
Taxation.Application/
  Features/<Area>/  commands, queries, validators and handlers per area (Setup, TaxFiles, Demands, Notices, Penalties,
                    Payments, Fees with clearances, Reports with the overview, Attention)
  Services/         TaxSetup (configuration in force), Ledger (dues, per-item payments, carrying, credits, reversal),
                    Assessor, CodeIssuer, CalendarProvider, Messenger (SMS, reference syncs), NoticeComposer,
                    TaxFileImporter, NoticeEscalation, DueNotices, DailyJob (singleton)
  Abstractions/     IPropertyDirectory, IHrmDirectory, INumberSource, IFileStorage
Taxation.Infrastructure/
  Data/Configuration/  EF mapping (SetupConfiguration, OperationsConfiguration)
  Data/TaxationAudit.cs   the audit-trail profile (kinds, codes, Under<TaxFile> for every child, skipped bulk rows)
  Data/TaxationJobs.cs    ISystemJobSource: the daily job (job_run), the last demand run and surcharge run
  Data/Seed/TaxationSeeder.cs   Data/PostgresNumberSource.cs (the schema's sequences)
  Clients/ServiceClients.cs     PropertyServiceClient, HrmServiceClient, ForwardAuthorizationHandler
  Storage/LocalFileStorage      letterhead logos under FileStorage:RootPath
Taxation.Api/  Endpoints/ (SetupEndpoints, TaxFileEndpoints, DemandNoticeEndpoints, MoneyEndpoints, ReportEndpoints),
               DailyJobService (hosted), TaxationExceptionHandler, TaxAuthorization, LenientEnumConverter
src/Tests/Taxation.Domain.Tests/  xUnit, tests named after the rules
```

## How the main flows work

- **Setup.** Towns are Property's master: a town is assigned to a circle from a date (`POST /tax-circles/{id}/towns`)
  with its short code (NG, printed as "Town"). Its tax files move with it. A rate set is a draft until approved. It holds
  each town's locality factors (land per sq yd, covered per sq ft, per category, as bands) and every other head's rates.
  Approval needs rates for every active configured regular head and factors for every town in a circle; the set it
  replaces ends the day before. A demand run uses the approved set in force on 1 July of its year, **and the annual
  increase compounds from that set's fiscal year**.
- **Heads.** Property tax (LTF formula, editable) and annual rent (from agreements) are built in. The admin defines other
  regular heads: calc BANDS / RATE / FIXED / FORMULA, applies to RES / COM / both, charged ALWAYS / from REGISTRATION / from
  a FACILITY date. Their rates live in the rate set; FORMULA heads keep the formula on the head, and the rate row only
  names what QTY counts. Every head printed on a notice has its own row on the notice table (a new layout version when
  one is added; there is no catch-all row). OTHER heads are either **fees** (charged case by case) or **charged on a
  notice** (`charged_on_notice`: `LEASE_MONEY`, `SECURITY`, `SURCHARGE`, `WHT_236A`) - a fee cannot be charged with one of
  those. `MISC` was removed (no "other" catch-all). The codes in `HeadCodes.System` are always seeded and cannot be
  deactivated. A head nothing refers to can be deleted (`TAX_SETUP.DELETE`); `POST /tax-heads/preview` works a head out
  for one file before it is saved. A rate set has `base_fiscal_year`: the increase compounds from that year (at most the
  first year the set is used; existing sets took the year of their start).
- **Circles.** The signing officer is the holder of the circle's TO staff row (the old `officer_post_id` became a staff
  row). A town is taken out of a circle from a date (`end`) or, when assigned by mistake and unused, voided. A circle
  nothing refers to can be deleted.
- **Tax files.** `POST /tax-files {propertyId}` reads Property: property, area summary, measurements, building plans,
  owners (and their contacts and addresses), leases, rentals, allotments. It stores a basis row, the payers and the
  agreements. Details:
  - A = measured land in sq yd plus regularized area.
  - Covered area: the latest approved non-completion plan (converted with the unit's factor), else the measured built-up area.
  - BPA date = that plan's approval date; completion = the approved `COMPLETION_PLAN`.
  - Category: from `category_map`, else RESIDENTIAL → RES and COMMERCIAL → COM by code, else it must be given. A category
    the map does not give, EXEMPT, or an uplift needs `TAX.APPROVE`.
  - Addressee: the tenant, else the lessee, else the largest owner, else the allottee. It can be changed.
  - Issues the file no. (`GDA-<TOWN>-COM-001`) and the constant 1Link reference (`PSID_PREFIX` + a sequence, 12 digits).
  - `POST /tax-files/{id}/sync` writes a new basis only when the facts changed, closes and opens payers, and adds agreements.
    It answers whether the basis changed, the category the map now gives and whether the property is still active; a
    pending notice is re-addressed to the current addressee.
  - Opening arrears (the old register's dues) are added before the first demand, corrected (`TAX.EDIT`) or removed
    (`TAX.DELETE`) while not billed; a payment before the first demand settles them.
  - An override or hold that has not started yet can be taken back (void).
  - Concurrency: `tax_file` and the money rows carry `xmin`; a change to a file's children touches the file, and money
    commands lock the file and its open demands (`SELECT … FOR UPDATE`) inside a transaction. A lost race is a 409.
- **Computation** (`TaxCalculator`, pure):
  - A hold open on 1 July suspends the year; an exempt or closed file is not assessed.
  - Completion uplift: once the grace years after the BPA date have passed without a completion certificate (held days
    don't count), the commercial factors (or residential, by setting / file) apply at `CC_UPLIFT_PCT_RES / _COM` (or the
    file's own %). `uplift_amount` is shown separately.
  - Registration and facility heads are charged for the share of the year from their date.
  - Bands pick the first band at or above the value; above the top band, the top rate stays or repeats per block.
  - Overrides: SPECIAL (fixed, optionally following the increase from its start, or a special policy) and MINIMUM (top-up).
  - Line invariant: amount = base + uplift + increase + override; `normal_amount` keeps what the formula gives.
- **Demand run.** Compute (`TAX.CREATE`) makes DRAFT demands for every active file of the scope and reports problems.
  Before computing, it applies the year-end surcharge to past years. A computed run of the same scope is cancelled and its
  drafts recomputed. Approve (`TAX.APPROVE`) then, for each draft:
  - moves the unpaid balances of earlier years onto it as `arrears` rows by year and head (surcharges under SURCHARGE);
    the earlier demand becomes CARRIED with `carried_amount` / `carried_to_demand_id`;
  - bills any unbilled opening arrears of the old register;
  - issues it, due on `DUE_DATE_DDMM` of the year, but never earlier than `DUE_DAYS_AFTER_ISSUE` (30) days after the day
    it is issued;
  - takes any credit on the file (paid beyond the charges after a correction) off it;
  - issues the FIRST notice with its SMS (not for a file under a hold) and records a reference sync.

  The approval answers the drafts issued under a hold without a notice, the drafts whose file changed since the run was
  worked out (`stale`: still issued as worked out) and the credit carried.

  `GET /demand-runs/{id}/problems` works the run's scope out again (nothing is saved) and lists the files that could not
  be computed, with why - the run itself keeps only the count.
- **Notices.**
  - Every notice is prepared PENDING with its variables pre-filled: payer, ID (old reference, else file no.), plot no.,
    town short code, address line, days, paragraph, title and the circle's office lines, frozen on the notice. It gets
    its endorsement no. only when confirmed (a cancelled pending notice uses none).
  - Review: printed fields change only with a reason (kept in `edited_fields`); the notice date, the days to pay (the
    pay-by date moves with them, in working days, unless one is given), the paragraph, payment method, paragraph / table
    and columns can change too.
  - Confirm (`TAX.EDIT`, or in bulk `TAX.APPROVE`; bulk issues ladder notices only and answers the ones it skipped)
    freezes the amount behind the reference, the table of particulars (`notice.particulars`), the EMVCo QR and the
    signing officer (holder of the circle's TO post in HRM), and queues the SMS. A ladder notice confirmed late is
    re-dated to the day of confirmation.
  - Escalation (`POST /notices/escalate`, `NoticeEscalation`) prepares the stage after the highest one reached
    (cancelled ones count) once its working-day count from the first notice has passed, for years not yet over; FIRST
    when a demand has none. The daily job does the same.
  - A notice created by hand (`POST /tax-files/{id}/notices`) is issued on the year's demand, or creates it (that, the
    amounts and Normal / Special need `TAX.APPROVE`; issuing at once needs `TAX.EDIT`). It can add earlier dues and the
    charges a notice carries by year and head (reason required). A year not started yet, a closed file and a year whose
    balance was carried are refused; a SURCHARGE notice needs an in-year surcharge on the year; the notice date is never
    before the demand's issue.
- **Penalties.** The year-end rule (10 %) applies once to every issued demand still owing after 30 June of its year; the
  demand becomes DEFAULTED. It runs from `POST /penalties/year-end` and at the start of every demand run. The in-year
  surcharge is previewed and applied by the Section: defaulters only (carrying arrears) or all unpaid, on the balance or
  the arrears part, once the rule's period has run. It adds a penalty, a SURCHARGE notice (pending) and a reference sync.
- **Payments** (`POST /payments`, `TAX.POST`):
  - Posted against the reference, settled oldest year first and item by item: arrears rows, then this year's lines,
    then surcharges; before the first demand, the unbilled opening arrears. Each piece is a `payment_allocation` row with
    its head. The payer's name is kept on the payment as on the day.
  - 1Link / QR / online payments need the bank's `transactionRef`, and posting the same one twice changes nothing
    (filtered unique index). Cash and bank counter need a receipt no. (MR-00001 is issued when not given; unique). Not
    more than is due; not dated before the file was opened.
  - SMS and reference sync after each payment.
  - **Reversal** (`POST /payments/{id}/reverse {reason}`, `TAX.REVERSE`, not by who posted it): a bounced cheque or a wrong reference. What it
    settled is owed again (a carried year's share goes back onto the demand it was carried into), the receipt stays,
    marked reversed, and a REVERSAL reference sync is recorded. A fee receipt's fee returns to due. Refunds are not built
    (Accounts).
  - The register filters by kind (dues / fee receipt), status, who took it, circle, party, fee, channel and dates, and
    searches receipt, transaction, reference, file no. and payer.
- **Fees and clearances.** A fee is quoted from the rate set in force: a flat amount, a rate per unit (from a band
  table when there is one), a percentage of a value, or the non-utilization fee of a plan extension (months / 12). A
  file's town and category rows win over general rows; a band's amount is rounded up as the rate set says. A fee is
  paid by a receipt (`POST /fees/{id}/pay`: a fee-kind payment with its MR- receipt, `TAX.POST`), can be charged and paid
  in one go (`payNow`) or charged exempt with a reason (`TAX.WAIVE`; refused while TAX_FEE_WAIVE is on). A paid water connection fee sets the WATER
  facility date. A clearance is issued only when nothing is behind the reference and no fee on the file is due; valid
  `CLEARANCE_VALID_DAYS` counting the day of issue; it keeps the payer's name; used once (`consume`, who and when kept) or
  revoked. A party can be deactivated (no new fee) or, with no fee ever, deleted. A party's CNIC of 13 digits is kept dashed as Property keeps it (`Guard.CnicNtn`), an
  NTN as typed; the parties search finds a CNIC or mobile however its dashes were typed.
- **Searches.** The payments register finds a payment by receipt, transaction, reference (typed with or without the
  spaces it is printed with) or the file no.; the notices register by notice / endorsement no., reference, file no. or
  the payer of its demand.
- **No 1Link connector and no SMS gateway exist yet**: reference syncs stay PENDING and SMS stay QUEUED (the bell
  counts both).
- **Daily job** (`DailyJob`, run by `DailyJobService` every 15 minutes once the day's hour has come, once a day): applies
  the year-end surcharge to years that are over and prepares the notices that fall due (`NoticeEscalation`). Each run is
  a `job_run` row (RUNNING / DONE / FAILED, counts, summary). `GET /daily-job` lists the recent runs; `POST
  /daily-job/run` (`TAX.RUN`, every town) runs it now; each run is one "Ran" line in the trail.
- **Attention** (`GET /attention`, any signed-in user, only the queues the caller may act on): `runs.computed`,
  `runs.problems`, `notices.pending`, `notices.unserved`, `notices.due`, `demands.overdue`, `demands.defaulted`,
  `yearend.pending`, `surcharge.due`, `clearances.expiring`, `fees.due`, `references.failed`, `references.pending`,
  `sms.queued`, `ratesets.draft`, `setup.blocking`. Each count matches a register filter (`/demands?overdue=true`,
  `?noticeDue=true`, `?yearEndDue=true`, `/demand-runs?status=COMPUTED`, `/reference-syncs?status=`, `/sms-messages?status=`,
  `/clearances?expiringWithinDays=7`).
- **Overview and reports** (`ReportEndpoints`): `GET /tax-overview?fiscalYear=` (files, the year's demands by status,
  demanded / recovered / owed, by circle, by month taxes and fees in Pakistan time, by head, notices, fees, clearances,
  largest balances, the in-year surcharge due, the last daily job), `GET /tax-reports/collection`, `/by-head`,
  `/by-circle-town`, `/arrears` (paged), `/collection-by-period?from&to&groupBy=day|month`; `GET /penalties` lists
  surcharges and `GET /penalty-runs/{id}` one run with its surcharges and notices.
- **Audit trail** (`audit_entry`, BuildingBlock/Audit, since 2026-10-21; `taxation.audit_log` is read-only, its rows copied
  in): every record added, changed or removed is written in the same save (`AuditTrailInterceptor`, profile in
  `TaxationAudit.cs`), one line per changed fact with the values before and after in words; every child of a tax file
  (Property facts, payers, head values, overrides, holds, agreements and revisions, arrears, demands, notices, surcharges,
  payments, fees, clearances) hangs under the file. Decisions add their own line with the reason (`IAuditTrail.Record`):
  Approved (demand run, rate set), Cancelled (run, notice - reason required), Reversed, Waived, Issued (notice, clearance),
  Revoked (reason required), Used (names who used a clearance: Taxation or Property), Recategorised / Exempted / Closed /
  Reopened (file), Corrected / Taken back (opening arrears, overrides, holds); a notice's printed-field edits are a line
  each with the reason. Runs write in `Quietly()` and record one Ran line (demand run worked out, surcharge run, year-end,
  issue pending notices, prepare due notices, the daily job - "System · Daily job" when scheduled). Not written line by
  line: demand_line, payment_allocation, psid_sync, sms_message, job_run. Endpoints: `GET /tax-files/{id}/history`
  (TAX.VIEW, scoped) and `GET /rate-sets/{id}/history` (any tax reader), shaped like `GET /audit`; `GET /audit`,
  `/audit/kinds` (IAM_AUDIT.VIEW) and `POST /audit/exports` (TAX.EXPORT) from `MapAuditTrail`; `GET /system/status`
  (IAM_SYSTEM.VIEW). The old `GET /audit-log` is gone.

## Seeds (`TaxationSeeder`)

- **Always ensured:** the system heads (the notice charges marked `charged_on_notice`; `MISC` retired when unused), a
  template for each notice stage, the two penalty rules (in-year 2 % after 3 months for defaulters; year-end 10 %), a
  letterhead and a notice table layout (the Section's 2nd notice of 16/09/2026) with a row for every head a notice carries.
- **First run only:**
  - circles C-I and C-II with their office lines;
  - conservancy, water and the fee heads;
  - the SMS texts;
  - the Section's five special-policy proposals, inactive;
  - a DRAFT rate set "Initial tariff (BOA 15.06.2023)" from 1 Jul 2025 with the conservancy, water and fee rates.
- Not seeded: towns and locality factors (assign towns, enter their factors, approve the set), the transfer fee (per
  town), category mappings beyond the two defaults, the circles' TO / Tc staff posts.

## Patterns to follow

- Everything in overview.md "Architecture" applies. Business rules live in domain methods; handlers load, call and save.
- **Money is whole rupees** (`Rupees.Round`). Every computed amount goes through it, as the Section's sheets do.
- **Cross-service reads go through `IPropertyDirectory` / `IHrmDirectory`**, never by touching another schema. A failure
  becomes `ExternalServiceException` (503 with a sentence). An HRM address left empty means weekends-only notice periods
  and no signing officer.
- **Reads in the same request see unsaved changes**: `Ledger` and the reference sync merge `DbSet.Local` with the
  database (`EntityLoaders.WithLocal`). Keep that when adding code that computes dues after a change.
- **Errors:** DomainException → 400; not found → 404; ExternalServiceException → 503; unique / FK violations → 409 with the
  sentences in `TaxationExceptionHandler.ConstraintMessages` (add one when you add a unique index).
- **Enums:** UPPER_SNAKE text in the database (`UpperSnake(n)`); names in JSON, read leniently (`LenientEnumConverter`).
- **Numbers:** sequences (`PostgresNumberSource`) for the reference, notice ref, demand, fee, clearance and receipt
  numbers. The endorsement no. counts per circle and year (max + 1; `notice_endst_no_key` is the backstop).

## Database and migrations

- The service migrates and seeds on startup (`Database:AutoMigrate`, on in Development).
- Migrations: `InitialTaxationSchema`; `AuditTrail` (2026-10-21: `audit_entry` with append-only triggers, the audit_log rows
  copied into it line by line, audit_log made read-only by a trigger, `tax_file_id` on notice / penalty /
  agreement_step_revision backfilled from their demand or agreement); `TaxationFixesOct2026` (xmin tokens, payment kind / payer / reversal columns,
  fee receipt link, clearance payer / consumed-by / revoked-by, `charged_on_notice`, `base_fiscal_year`, penalty rule
  versions keyed by code and date, filtered unique transaction ref / receipt no., `job_run`, `audit_log`; data: the
  circles' officer posts became TO staff rows, `base_fiscal_year` = the FY of each set's start, `MAN-` refs cleared,
  fee source `MISC` cleared, `charged_on_notice` set on the system notice heads).
- New migration (from `src/`):
  `dotnet ef migrations add <Name> -p Services/Taxation/Taxation.Infrastructure -s Services/Taxation/Taxation.Api -o Data/Migrations`.
  It needs an SDK with the ASP.NET Core runtime.
- Keep `docs/gda_taxation_module.dbml` in step with any schema change.

## Configuration

- `Services:PropertyBaseUrl` (`http://localhost:5141`; compose `http://poperty-management-api:8080`) and
  `Services:HrmBaseUrl` (`http://localhost:5144`; compose `http://hrm-api:8080`), `Services:TimeoutSeconds` (20).
- `FileStorage:RootPath`: `storage` by default (gitignored). In compose it is the `taxation-files` volume.
- Settings that change with Board decisions are data (`/tax-settings`, effective-dated; a value from a later date can be
  taken back, `DELETE /tax-settings/{key}/{effectiveFrom}`; `GET /tax-settings/{key}/history`), not configuration. A
  save that changes nothing makes no new version (settings, penalty rules, policies).
- `Taxation:DailyJob:Enabled` (true) and `Taxation:DailyJob:HourPkt` (6): the daily job and the hour, Pakistan time,
  from which it runs.

## Testing

`dotnet test src/ERP.slnx` runs `src/Tests/Taxation.Domain.Tests`. `Fixture.cs` builds the heads, an approved rate set
with Nathiagali's factors and a tax file from a Property snapshot (400 sq yd, 2,500 sq ft covered: property tax
Rs 14,500). Add a test for every new rule, named after it. For API behaviour, run Identity, Property, HRM and Taxation
against Postgres and step through `Taxation.Api.http`. The frontend's functional suites (`ERP-Frontend/tools/e2e/
taxation-api.e2e.mjs` and `taxation-ui.e2e.mjs`) drive the whole contract on standing E2E fixtures in Thandiani
(Circle I) - see `taxation-fixtures.mjs` there.

## Gotchas

- `DomainException` wraps its message; `DomainMessages.Text` gives the bare sentence for problem lists.
- The FY of a demand is the year it starts in; `FiscalYear.Label(2026)` is "2026-27".
- A rate set is fixed once approved: start a new one from it (`copyFromId`).
- A town assigned from a future date moves its files on their next sync.
- A hold's `ended_on` is its LAST day and is covered (`LitigationHold.Covers`): notices and the demand resume the day
  after. A new hold must start after every earlier hold's last day; only one hold may be without a last day.
- Once a demand is paid up, the notice context proposes FIRST again for the next notice on it.
- `PenaltyRuleInput.RateSetId` is a plain `Guid?` (the handler converts it); a strongly typed id there made System.Text.Json
  refuse the request ("wrong type or format").
- Penalty rules are versions per code (`penalty_rule_code_effective_from_key`): `PUT /penalty-rules/{id} {rule,
  effectiveFrom}` adds one from a date; `GET /penalty-rules?history=true` lists them; a later one is taken back with
  `DELETE /penalty-rules/{id}`. Surcharges keep the rule version they were applied with.
- System.Text.Json cannot key a dictionary by an enum read with `LenientEnumConverter` - return lists. EF cannot translate
  `IReadOnlySet.Contains` or an enum-array `Contains` with a converter - use a `List` or explicit ORs.
- An interpolated string cannot hold `global::` (the colon starts a format) - use a local.
- Notice letterhead and table layout are versions; a change on the same day as the version in force replaces it (the
  replaced row ends the day before it starts and is never picked again; issued notices keep theirs).
