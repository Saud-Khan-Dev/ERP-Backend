# HRM service: context for AI assistants

Read [overview.md](overview.md) first for the stack and shared conventions. This file covers `src/Services/HRM` only.

## What it is

The GDA HRMS, in one service:

- the organization (units, sanctioned posts, pay scales) and the employee master with service history and HR actions;
- attendance, leave and performance (PER) reviews;
- the salary structure, income tax, loans, GP Fund, the payroll engine and salary payments;
- tasks, employee requests, reports and the employee portal (`/me`).

- Port `http://localhost:5144` (launchSettings) / `5144:8080` in compose (`hrm-api`), gateway prefix `/hrm-service/`.
- Database `ERP_DB`, schema **`hrms`**, snake_case, native Postgres enums, history in `hrms.__EFMigrationsHistory`.
- OpenAPI at `/openapi/v1.json` (about 340 operations). Every route needs a token. There is no anonymous endpoint.

**Schema source: [`docs/gda_hrms_schema.sql`](../gda_hrms_schema.sql)**, the client's SQL.
[`docs/gda_hrms_schema.dbml`](../gda_hrms_schema.dbml) holds the same schema for dbdiagram.io. The single migration
`InitialHrmsSchema` builds it, plus the raw SQL in `Data/Sql/HrmsSchemaSql.cs`. A catalog diff against the SQL file
found only these differences, all on purpose:

- **Deferred constraints.** Every EXCLUDE constraint is `DEFERRABLE INITIALLY DEFERRED`. The three partial unique
  indexes `uq_employee_contact_primary`, `uq_eba_one_primary` and `uq_edu_one_highest` are deferrable btree exclusions
  with the same names, so a flag or a period can move between rows in one save.
- **Post capacity.** The check is a deferred constraint trigger that locks the post row.
- **Business triggers moved to C#** (the "hybrid" choice). The run status workflow, loan posting on finalize, task
  progress mirroring, the payment bank snapshot and the request and goal-weight checks are domain code. The guard
  triggers stay:
  - append-only ledgers;
  - `updated_at`;
  - a finalized run's rows are locked (`fn_block_if_run_locked`; a slip's hold / release may still change);
  - the payslip snapshot is frozen;
  - `fn_days_payable` is kept for reports.
- **Roles.** The access roles (security section 3 of the file) are a DBA step, not part of the migration.
- **UNIQUE constraints** are unique indexes with the same names.

## Permissions

Four modules in `PermissionCatalog` (the Identity seeder reads it; the frontend's permission mirror needs the same codes):

| Module | Codes | Used for |
| --- | --- | --- |
| `HR` | VIEW, CREATE, EDIT, DELETE, APPROVE | employees, documents, service history, assignments, HR actions (APPROVE approves and applies), separations, performance reviews (APPROVE finalizes), tasks, employee requests (APPROVE decides), people reports |
| `HR_SETUP` | VIEW, CREATE, EDIT, DELETE | catalogues (unit types, locations, designations, document types, recruitment methods, event types, request types, BPS names), org units, pay scales, posts, performance periods |
| `ATTENDANCE` | VIEW, CREATE, EDIT, DELETE, APPROVE | shifts, holidays, attendance, leave types, entitlements, applications (APPROVE approves / rejects, posts ledger rows, runs the year-end), leave report |
| `PAYROLL` | VIEW, CREATE, EDIT, DELETE, APPROVE, POST | components, rules (APPROVE), overrides, tax, loans (APPROVE sanctions / waives / writes off), GP Fund, bank accounts, pay records, payroll (APPROVE approves runs, holds salaries; POST finalizes, reverses, pays, locks months, credits GPF interest), pay reports |

Readers of the structure (org units, posts, pay scales, shifts) need any one HRM module (`HrmAuthorization.AnyHrmReader`).
Catalogues, holidays and leave types are readable by any signed-in user. `/me/*` needs only a token whose account is
linked to an employee (the `emp` claim, else `employee.user_id`); an unlinked account gets 403.

## Layout

```text
HRM.Domain/
  Abstraction/  Entity<TId> (audit columns), Aggregate<TId>, ITypedId<TSelf>   Exceptions/  DomainException
  Common/       Guard, Cnic (35202-1234567-1), Iban (PK, mod-97), DateRange, EnumText (enum names as words)
  Enums/        HrmsEnums.cs: one enum per Postgres type, members in the schema's label order
  ValueObjects/ one typed id per table: EmployeeId.Of(guid), EmployeeId.New()
  Models/<Area> Organization, PayScale, Post, Employee, Document, Service, Performance, Attendance, Leave, Salary,
                Tax, Loan (with GP Fund), Payroll, Task (tasks and requests)
  Services/     PayrollCalculator, PayFormula, PayTiers (payroll engine); GpFundInterest; OrganizationTree
  Views/        keyless read models over the five v_* views
HRM.Application/
  Features/<Area>/   commands, queries, validators and handlers per area (several use cases per file, grouped by area)
  Services/          shared application services (below)
  Dtos/, Options/ (Documents, Hrm, Payroll sections), Data/IApplicationDbContext
HRM.Infrastructure/
  Data/Configuration/  EF mapping, one file per area, names exactly as in the SQL
  Data/Sql/HrmsSchemaSql.cs   EXCLUDEs, functions, triggers, views, comments (called by the migration)
  Data/Seed/HrmSeeder.cs      seeds (below)    Data/DatabaseInitializer.cs   wait for DB, migrate, reload types, seed
  Storage/LocalFileStorage    documents and photos under FileStorage:RootPath
HRM.Api/  Endpoints/ (Carter modules), HrmExceptionHandler, HrmAuthorization, LenientEnumConverter
src/Tests/HRM.Domain.Tests/  xUnit, ~100 tests named after the rules
```

Application services: `HrLookup` (batch labels: names, placements, posts, units, subtrees, employees in a unit),
`ServiceRecordReader` / `ServiceRecordWriter` (posts held; appointments, moves with pay fixation, separations),
`HrActionApplier`, `PayService` (scales and pay records), `CodeIssuer` (EMP-###, POST-####), `FileUploads`,
`WorkCalendar` (an employee's working days: shift weekdays, gazetted and local holidays), `LeaveService` (days and
balances), `PayrollInputBuilder`, `PayrollEngine` and `PayslipBuilder` (payroll), `CurrentEmployee` (self-service).

## How the main flows work

- **Effective dating everywhere.** Org units, posts, pay scales, pay records, shifts, salary rules and overrides have
  `effective_from / effective_to`. A change from a date closes the current row the day before and opens a new one.
  A correction edits the latest row in place. History is never overwritten.
- **Employees.** Registration is one request: master record, contacts, addresses, family and, optionally, the
  appointment. The appointment writes the service-history row, the regular assignment and the starting pay. Numbers
  are issued as `EMP-###` when left blank.
- **HR actions.** draft → pending → approved → applied. Applying writes everything the action means: transfer,
  promotion with pay fixation, LWOP, suspension, retirement and so on. The service history is append-only.
- **Attendance.** Recorded, imported by employee number, or created by "close day". Close day marks everyone holding a
  post without a record as on leave, holiday, weekend or absent. Future dates are refused.
- **Leave.** Balance = entitlement + the signed `leave_ledger`. Applying checks overlap and the days available (pending
  applications count against them). Approving writes usage rows per calendar year and turns closed absences into
  leave. Cancelling reverses the ledger rows. The year-end is idempotent: a carry-forward or a lapse per balance.
- **Performance.** Periods; reviews opened one by one or in bulk. The evaluator in bulk is the holder of the post's
  reporting post. draft → submitted → acknowledged → finalized; weights must total 100; the score and PER category
  are worked out from the goals.
- **Payroll engine** (`PayrollCalculator`, pure, unit-tested):
  - The month is split wherever the post, the post version or the pay record changes.
  - Earnings are prorated by payable days; LWOP and days without a regular post are not paid.
  - Allowances come from the best rule: lowest priority number, then the most specific scope, then the latest start.
    An employee override replaces the rule. Rules are fixed, percentage, formula (`PayFormula`, a safe parser) or
    tiered (`PayTiers`, JSON bands).
  - Deductions are whole-month: rule deductions, GP Fund subscription, loan installments due (by priority), then
    income tax. Tax is the projected annual tax less what was withheld, spread over the months left in the tax year.
  - Net pay is kept ≥ 0 by dropping loans, then GPF, then rule deductions, with a note on the slip.
- **Payroll runs.**
  - Runs move draft → calculated → reviewed → approved → finalized → paid; each can step back one stage before
    finalization.
  - Maker-checker: `Payroll:RequireSeparateApprover`, on by default.
  - Calculation can be repeated and keeps hand-entered adjustments.
  - **Finalize** runs in one DB transaction. It writes the tax ledger, loan recoveries, GPF subscriptions and advance
    recoveries, then issues the frozen JSON payslips and flips the status. The ledgers must be written before the
    flip, because the lock triggers refuse them afterwards.
  - **Reverse** is refused once a payment went out (it must be recorded as returned first). It undoes the recoveries:
    a recovered installment is closed and **re-issued** as a new installment, because `installment_id` is unique in
    `payroll_loan_deduction`. GPF rows are offset with adjustments. A replacement run then references the reversed one.
  - Regular runs: one live regular run per month. Supplementary runs pay those on no other run of the month. Arrears,
    bonus and final-settlement runs are made of adjustments; the engine adds only the extra tax they cause.
- **Payments.** Generated per finalized run into the primary bank account, which is snapshotted on the payment.
  Processed with the bank reference; failed or returned payments are re-generated. The bank advice groups payments by
  bank. A run is marked paid when every slip not held has a processed payment.
- **Loans and GP Fund.** Sanction lays out the installment schedule; the last installment absorbs the rounding. A GPF
  advance is paid out of the fund and its recoveries go back into it. Repayments outside payroll, waivers and
  cancellations are refused while a pending run is deducting the loan. GPF interest for a fiscal year is rate / 12 on
  each month-end balance, posted once per account.

## Seeds (`HrmSeeder`)

- **Always ensured:** BPS 1–22, the system service-event types, and the system components BASIC / IT / GPF. The
  payroll engine depends on all of these, so they can't be deactivated.
- **First run only:** org unit types, recruitment methods, 8 document types (no "Other"), 13 salary components, the MCA
  and GPF-advance loan types, 4 leave types and 3 request types. Everything else (designations, locations, scales,
  posts, rules, tax years) is entered through the API.

## Patterns to follow

- Everything in overview.md "Architecture" applies.
- **Business rules belong in domain methods.** Handlers load, call the domain, and save. A rule that needs data the
  aggregate doesn't hold (balances, seats, other rows) is passed in: e.g. `LeaveApplication.Apply(..., others)` and
  `Post.Revise(..., filledSeats)`.
- **Errors.**
  - `DomainException` → 400, with the wrapper text the frontend strips. `DomainMessages.Text(e)` gives the bare
    sentence for batch "problems" lists.
  - Not-found exceptions → 404. `EmployeeProfileRequiredException` → 403.
  - Postgres unique / exclusion / FK violations → 409 and CHECK violations → 400. `HrmExceptionHandler` maps
    constraint names to sentences: add an entry when you add a constraint.
- **Enums in JSON.** Responses carry the name (`OnLeave`). Requests accept the name or the schema's snake_case label
  (`on_leave`), case-insensitively (`LenientEnumConverter`). Query-string enums go through `QueryParsing.ParseEnum`.
  Numbers are refused.
- **Self-service** reuses the HR queries and commands with an `OnlyForEmployee` / `ReviewActor` parameter. Someone
  else's row is reported as 404, not 403.
- **Lists:** `QueryParsing.Page` and `PaginatedResult<T>`. Labels come from `HrLookup` in a fixed number of queries.
  Never query once per row.
- **Name uniqueness in catalogues** is checked case-insensitively in the handler; the DB unique index is the backstop.
- **Batch operations** (import, grant entitlements, start reviews, assign shifts, generate payments) save the good rows
  and return the bad ones in `problems`.

## Database and migrations

- The service migrates and seeds on startup (`Database:AutoMigrate`, on in Development).
- Npgsql writes enum labels alphabetically. After `migrations add`, reorder the `Npgsql:Enum:hrms.*` annotations to the
  C# declaration order (the schema's order) and add `HrmsSchemaSql.Up / Down` calls.
- **Never edit `HrmsSchemaSql` after its migration ships.** Later raw SQL goes in a new class called from a new
  migration.
- New migration (from `src/`):
  `dotnet ef migrations add <Name> -p Services/HRM/HRM.Infrastructure -s Services/HRM/HRM.Api -o Data/Migrations`.
  On this machine `dotnet ef` needs the ASP.NET runtime workaround from the assistant memory.
- Keep `docs/gda_hrms_schema.sql` and the DBML in step with any schema change, and list new deliberate differences in
  the comment at the top of `HrmsSchemaSql` and in this file.

## Configuration

- `FileStorage:RootPath`: `storage` by default (gitignored). In compose it is the `hrm-files` volume.
- `Documents`: 25 MB per document; pdf, images and office files; photos jpg / png / webp up to 5 MB.
- `Hrm`: employee number prefix `EMP` with 3 digits, post code prefix `POST` with 4 digits, retirement age 60, annual
  increment on 1 December.
- `Payroll:RequireSeparateApprover`: true. Compose and `appsettings.Development.json` turn it off for one-person testing.

## Testing

`dotnet test src/ERP.slnx` runs `src/Tests/HRM.Domain.Tests`. `Fixture.cs` builds employees, components, rules, a
tax year (FY 2025-26 salaried slabs) and loans. Add a test for every new domain rule, named after the rule, e.g.
`Net_pay_never_goes_below_zero_the_least_important_loan_is_left_for_later`.

## Gotchas

- `employee.full_name` is generated by Postgres: never assign it. Use `DisplayName` on an unsaved employee.
- Inside LINQ query syntax, `from` is a keyword: don't name locals `from` / `to` in methods that use `from x in ...`.
- `fn_block_if_run_locked` refuses tax-ledger, slip, line, adjustment and loan-deduction writes once a run is
  finalized, paid or reversed. Write them before the status changes, in the same transaction.
- Superseded installments (after a reversal) are stored as `waived` with nothing paid. `installment_status` has no
  "superseded" label; the re-issued installment carries the amount.
- Tax years cover 1 July to 30 June. The payroll uses the active tax year that covers the period's last day; without
  one, no tax is withheld and the calculation says so.
- The schema's enum types keep their `other` labels (gender, adjustment type …). The seeds add no "Other" catalogue
  rows.
- `v_gp_fund_balance.total_subscribed` counts subscription rows only, so a reversed run's subscription still shows
  there (the balance is right; the offset is an adjustment).
