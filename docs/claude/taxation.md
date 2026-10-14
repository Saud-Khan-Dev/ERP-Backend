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
`agreement_schedule_line`, `integration_inbox` / `integration_outbox`, `sync_checkpoint`, `import_batch` / `import_row`
and `audit_log`. The columns that point at them (`payment.challan_id`, `demand_line.schedule_line_id`,
`arrears.import_row_id`) are plain uuids for now. `notice.sms_message_id` has no foreign key on purpose
(`sms_message.notice_id` points back; both are written in one save).

## Permissions

| Module | Codes | Used for |
| --- | --- | --- |
| `TAX` | VIEW, CREATE, EDIT, DELETE, APPROVE, POST | tax files, demands, notices, surcharges, payments, fees, clearances. CREATE opens files, computes runs, creates notices and fees; EDIT syncs files and reviews, confirms, serves and escalates notices; APPROVE approves demand and surcharge runs, sets overrides, holds and categories, issues pending notices in bulk, waives fees and issues clearances; POST receives payments and fee receipts |
| `TAX_SETUP` | VIEW, CREATE, EDIT, DELETE, APPROVE | circles, heads, category map, rate sets (APPROVE approves one), settings, special policies, penalty rules, templates, letterhead, notice table |

Setup lists are readable with `TAX.VIEW` or `TAX_SETUP.VIEW` (`TaxAuthorization.AnyTaxReader`). **Calls that read
Property (open and sync a file, assign a town, map a property type) pass the caller's token on, so the caller also needs
`PROPERTY.VIEW`.** Holidays and post holders are read from HRM the same way.

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
  Services/         TaxCalculator (the annual computation), TaxFormula (safe formula parser), Numbers, PaymentQr,
                    WorkCalendar, NoticeTable (the table of particulars)
Taxation.Application/
  Features/<Area>/  commands, queries, validators and handlers per area (Setup, TaxFiles, Demands, Notices, Penalties,
                    Payments, Fees with clearances and the report)
  Services/         TaxSetup (configuration in force), Ledger (dues, per-item payments, carrying), Assessor,
                    CodeIssuer, CalendarProvider, Messenger (SMS, reference syncs), NoticeComposer, TaxFileImporter
  Abstractions/     IPropertyDirectory, IHrmDirectory, INumberSource, IFileStorage
Taxation.Infrastructure/
  Data/Configuration/  EF mapping (SetupConfiguration, OperationsConfiguration)
  Data/Seed/TaxationSeeder.cs   Data/PostgresNumberSource.cs (the schema's sequences)
  Clients/ServiceClients.cs     PropertyServiceClient, HrmServiceClient, ForwardAuthorizationHandler
  Storage/LocalFileStorage      letterhead logos under FileStorage:RootPath
Taxation.Api/  Endpoints/ (SetupEndpoints, TaxFileEndpoints, DemandNoticeEndpoints, MoneyEndpoints), TaxationExceptionHandler,
               TaxAuthorization, LenientEnumConverter
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
  names what QTY counts. A new head gets its own row on the notice table (a new layout version). OTHER heads are fees
  and the charges a notice can add (`LEASE_MONEY`, `SECURITY`, `SURCHARGE`, `WHT_236A`, `MISC`). The codes in
  `HeadCodes.System` are always seeded and cannot be deactivated.
- **Tax files.** `POST /tax-files {propertyId}` reads Property: property, area summary, measurements, building plans,
  owners (and their contacts and addresses), leases, rentals, allotments. It stores a basis row, the payers and the
  agreements. Details:
  - A = measured land in sq yd plus regularized area.
  - Covered area: the latest approved non-completion plan (converted with the unit's factor), else the measured built-up area.
  - BPA date = that plan's approval date; completion = the approved `COMPLETION_PLAN`.
  - Category: from `category_map`, else RESIDENTIAL → RES and COMMERCIAL → COM by code, else it must be given.
  - Addressee: the tenant, else the lessee, else the largest owner, else the allottee. It can be changed.
  - Issues the file no. (`GDA-<TOWN>-COM-001`) and the constant 1Link reference (`PSID_PREFIX` + a sequence, 12 digits).
  - `POST /tax-files/{id}/sync` writes a new basis only when the facts changed, closes and opens payers, and adds agreements.
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
  - issues it, due on `DUE_DATE_DDMM` of the year;
  - issues the FIRST notice with its SMS and records a reference sync.
- **Notices.**
  - Every notice is prepared PENDING with its variables pre-filled: payer, ID (old reference, else file no.), plot no.,
    town short code, address line, days, paragraph, title and the circle's office lines, frozen on the notice.
  - Review: printed fields change only with a reason (kept in `edited_fields`); payment method, paragraph / table and
    columns can change too.
  - Confirm (`TAX.EDIT`, or in bulk `TAX.APPROVE`) freezes the amount behind the reference, the table of particulars
    (`notice.particulars`), the EMVCo QR and the signing officer (holder of the circle's officer post in HRM), and queues
    the SMS.
  - Escalation (`POST /notices/escalate`) prepares SECOND / FINAL / SHOW_CAUSE once their working-day count from the first
    notice has passed, for years not yet over.
  - A notice created by hand (`POST /tax-files/{id}/notices`) is issued on the year's demand, or creates it. It can switch
    heads Normal / Special and add earlier dues and other charges by year and head (reason required). A year whose
    balance was carried is refused with the year to use instead.
- **Penalties.** The year-end rule (10 %) applies once to every issued demand still owing after 30 June of its year; the
  demand becomes DEFAULTED. It runs from `POST /penalties/year-end` and at the start of every demand run. The in-year
  surcharge is previewed and applied by the Section: defaulters only (carrying arrears) or all unpaid, on the balance or
  the arrears part, once the rule's period has run. It adds a penalty, a SURCHARGE notice (pending) and a reference sync.
- **Payments** (`POST /payments`, `TAX.POST`):
  - Posted against the reference, settled oldest year first and item by item: arrears rows, then this year's lines,
    then surcharges. Each piece is a `payment_allocation` row with its head.
  - 1Link / QR / online payments need the bank's `transactionRef`, and posting the same one twice changes nothing. Cash
    and bank counter need a receipt no. (MR-00001 is issued when not given). Not more than is due.
  - SMS and reference sync after each payment.
- **Fees and clearances.** A fee is quoted from the rate set in force: a flat amount, a rate per unit (from a band
  table when there is one), a percentage of a value, or the non-utilization fee of a plan extension (months / 12). A
  file's town and category rows win over general rows. A paid water connection fee sets the WATER facility date. A
  clearance is issued only when nothing is behind the reference; valid `CLEARANCE_VALID_DAYS`.
- **No 1Link connector and no SMS gateway exist yet**: reference syncs stay PENDING and SMS stay QUEUED.

## Seeds (`TaxationSeeder`)

- **Always ensured:** the system heads, a template for each notice stage, the two penalty rules (in-year 2 % after
  3 months for defaulters; year-end 10 %), a letterhead and a notice table layout (the Section's 2nd notice of 16/09/2026).
- **First run only:**
  - circles C-I and C-II with their office lines;
  - conservancy, water and the fee heads;
  - the SMS texts;
  - the Section's five special-policy proposals, inactive;
  - a DRAFT rate set "Initial tariff (BOA 15.06.2023)" from 1 Jul 2025 with the conservancy, water and fee rates.
- Not seeded: towns and locality factors (assign towns, enter their factors, approve the set), the transfer fee (per
  town), category mappings beyond the two defaults, the circles' officer posts.

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
- One migration so far: `InitialTaxationSchema`.
- New migration (from `src/`):
  `dotnet ef migrations add <Name> -p Services/Taxation/Taxation.Infrastructure -s Services/Taxation/Taxation.Api -o Data/Migrations`.
  It needs an SDK with the ASP.NET Core runtime.
- Keep `docs/gda_taxation_module.dbml` in step with any schema change.

## Configuration

- `Services:PropertyBaseUrl` (`http://localhost:5141`; compose `http://poperty-management-api:8080`) and
  `Services:HrmBaseUrl` (`http://localhost:5144`; compose `http://hrm-api:8080`), `Services:TimeoutSeconds` (20).
- `FileStorage:RootPath`: `storage` by default (gitignored). In compose it is the `taxation-files` volume.
- Settings that change with Board decisions are data (`/tax-settings`, effective-dated), not configuration.

## Testing

`dotnet test src/ERP.slnx` runs `src/Tests/Taxation.Domain.Tests`. `Fixture.cs` builds the heads, an approved rate set
with Nathiagali's factors and a tax file from a Property snapshot (400 sq yd, 2,500 sq ft covered: property tax
Rs 14,500). Add a test for every new rule, named after it. For API behaviour, run Identity, Property, HRM and Taxation
against Postgres and step through `Taxation.Api.http`.

## Gotchas

- `DomainException` wraps its message; `DomainMessages.Text` gives the bare sentence for problem lists.
- The FY of a demand is the year it starts in; `FiscalYear.Label(2026)` is "2026-27".
- A rate set is fixed once approved: start a new one from it (`copyFromId`).
- A town assigned from a future date moves its files on their next sync.
- Notice letterhead and table layout are versions; a change on the same day as the version in force replaces it (the
  replaced row ends the day before it starts and is never picked again; issued notices keep theirs).
