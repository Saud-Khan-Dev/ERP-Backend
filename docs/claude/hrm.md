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
- OpenAPI at `/openapi/v1.json` (about 345 operations). Every route needs a token except `GET /directory/holidays` (gazetted
  holidays are public; Taxation's daily job reads them without a token).

**Schema source: [`docs/gda_hrms_schema.sql`](../gda_hrms_schema.sql)**, the client's SQL.
[`docs/gda_hrms_schema.dbml`](../gda_hrms_schema.dbml) holds the same schema for dbdiagram.io. The migration
`InitialHrmsSchema` builds it, plus the raw SQL in `Data/Sql/HrmsSchemaSql.cs`; later migrations add to it (see
"Database and migrations") and are folded into the SQL file. A catalog diff against the SQL file found only these
differences, all on purpose:

- **Deferred constraints.** Every EXCLUDE constraint is `DEFERRABLE INITIALLY DEFERRED`. The three partial unique
  indexes `uq_employee_contact_primary`, `uq_eba_one_primary` and `uq_edu_one_highest` are deferrable btree exclusions
  with the same names, so a flag or a period can move between rows in one save.
- **Post capacity.** The check is a deferred constraint trigger that locks the post row and counts the busiest day of
  the new holding (holders who follow one another share a seat).
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

Seven catalogue modules (`PermissionCatalog`; the Identity seeder grants each new code to the roles that held the old one):

| Module | Codes | Used for |
| --- | --- | --- |
| `HR` | VIEW, CREATE, EDIT, DELETE, APPROVE, EXPORT, PAY, RUN | employees, documents, service history, assignments, HR actions (APPROVE approves / returns / rejects; EDIT applies), separations (APPROVE), performance reviews (APPROVE finalizes, countersigns), tasks, requests (APPROVE decides). PAY: a pay change and the next increment date. RUN: the annual increment and pay revisions. EXPORT: HR CSVs and reports (the screens; `POST /audit/exports`) |
| `HR_SENSITIVE` | VIEW, EDIT | VIEW: CNIC, date of birth, contacts, addresses, emergency contacts, family, pay figures, bank account numbers (see "Personal facts"). EDIT: change a CNIC or date of birth; every salary bank account write |
| `HR_SETUP` | VIEW, CREATE, EDIT, DELETE | catalogues, org units, pay scales, posts, performance periods |
| `ATTENDANCE` | VIEW, CREATE, EDIT, DELETE, APPROVE, EXPORT, RUN, ADJUST | shifts, holidays, attendance, leave types, entitlements, applications (APPROVE decides and cancels an approved leave). RUN: close a day, import attendance, grant the year's entitlements, close the leave year. ADJUST: a manual leave-ledger entry |
| `PAYROLL` | VIEW, CREATE, EDIT, DELETE, APPROVE, POST, EXPORT, REVERSE, WAIVE, ADJUST | runs (APPROVE approves, holds salaries; POST finalizes, pays, locks; REVERSE reverses a run and fails / returns a payment; DELETE deletes a run or an open month), loans (CREATE enters an application; APPROVE sanctions, cancels, records repayments; WAIVE waives an installment or writes a loan off), GP Fund, tax. ADJUST: payslip adjustments, salary overrides, tax openings. EXPORT: payroll CSVs, slip print, the bank advice (`GET /payroll-runs/{id}/bank-advice` needs it) |
| `PAYROLL_SETUP` | VIEW, CREATE, EDIT, DELETE | salary components and rules, tax years and slabs, GP Fund interest rates, loan types (reads also accept PAYROLL.VIEW) |
| `IAM_USERS.EDIT` | | linking an employee to a sign-in account (`POST/DELETE /employees/{id}/user`) |

Readers of the structure need any one HRM module (`HrmAuthorization.AnyHrmReader`). Contacts, addresses, emergency
contacts and family need HR.EDIT **and** HR_SENSITIVE.VIEW to change. `GET /attention`, `GET /me/link` and the directory
need only a signed-in token. `/me/*` needs a token carrying the `emp` claim (see Self-service).

## Administration rules (2026-10-21 overhaul)

- **Office scope.** `ICurrentUser.OfficeScope` (org unit ids; null = every office) limits a person to the employees whose
  post is in one of those units or below them (the post held today, else the last one held, else the first one to come;
  never-placed employees are outside every scope). `OfficeScope` works it out once per request (`OfficeScopeBehavior`,
  a MediatR behavior) into `EmployeeScope`, and `ApplicationDbContext` applies it as a **global query filter** on every
  table with an `EmployeeId` (employee, contacts, family, bank accounts, pay records, documents, education, HR actions,
  separations, reviews, shifts, attendance, leave, overrides, tax, loans, GP Fund, slips, payments, tasks, requests and
  the leave / GPF / tax views). Post assignments, the service book and structure are not filtered (seats are counted for
  everyone). So lists, reports, attention and loaders stop at the offices; a record outside answers 404. Bulk runs
  (annual increment, close day, entitlements, year end, start reviews, GPF interest) cover the person's offices.
  Whole-run payroll acts (create, calculate, review, approve, finalize, reverse, pay, months, arrears, payments) are
  refused unless the person works on every office (`WholeOrganizationRefusalAsync`). `/me` and `/directory` run
  unfiltered (`AllOffices()` endpoint filter); so do numbering and uniqueness checks (`EmployeeScope.Unrestricted()`).
  A scoped person registers employees only with an appointment to a post in their offices.
- **Personal facts.** Without HR_SENSITIVE.VIEW responses carry these as null and name them in `restricted`
  (`Sensitive`): employee list (cnic, dateOfBirth, mobile; CNIC search is off) and record (cnic, dateOfBirth, contacts,
  addresses, emergencyContacts, familyMembers, basicPay), pay records (basicPay), bank accounts (accountNumber, iban),
  slips list, workings, payslip (`PayslipDto.Withheld()`), pay register rows, payments and bank advice lines (pay,
  accountNumber, iban), and the employee History (old / new / detail of personal and pay lines). Own records through
  `/me` are always whole. A person who cannot see CNIC / DOB sends them null on "Edit personal details" and they stay.
- **Separation of duties** (`DutyGuard` over `IDutyRules`; every rule on unless a Super Admin switched it off in
  Administration › Configuration; a refusal is a 400 sentence and a `Refused` line in the trail):
  OWN_RECORD (the caller's `emp` = the target employee: pay change, next increment, overrides, tax opening / exemptions,
  bank accounts, adjustments, hold / release, taking a slip off a run, loan sanction / repayment / waive / cancel /
  write-off, GP Fund entries and closing, HR action approve / return / reject / apply, separation and its settlement,
  document verify / reject, request decide, leave decide, leave-balance entry, personal details and contacts, number
  correction, review countersign / finalize; bulk increments skip the runner's own record), HR_ACTION (drafter /
  submitter ≠ approver), PAYROLL_APPROVE (preparer, reviewer, adjusters ≠ approver), PAYROLL_PAY (approver ≠ who
  finalizes, prepares or records payments, marks paid), LOAN_SANCTION (who entered the application ≠ who sanctions it;
  `employee_loan.created_by`), LEAVE_APPROVE (who keyed the application in ≠ who decides it;
  `leave_application.created_by`). The old `Payroll:RequireSeparateApprover` / `Hrm:RequireSeparateApprover` settings are
  gone; `GET /settings` reports the rules (`dutyRules`, and `requireSeparate*` from HR_ACTION / PAYROLL_APPROVE).
- **Loans are two steps.** `POST /employees/{id}/loans` (PAYROLL.CREATE) enters an application (status `pending`, no
  installments); `POST /loans/{id}/sanction` (PAYROLL.APPROVE) lays out the schedule (and pays a GP Fund advance out);
  `cancel` turns an application down. `sanctionNow: true` does both at once only while LOAN_SANCTION is off.
- **Audit trail** (BuildingBlock/Audit; table `hrms.audit_entry`, append-only by triggers; module "Human Resources").
  `AuditTrailInterceptor` writes a line per record added / changed / removed (FKs as names); `HrmAuditProfile` gives the
  kinds and hangs every employee record `Under<Employee>` (grandchildren under their loan, slip, review, GP account,
  task: `GET /employees/{id}/history` gathers them). Skipped: slip segments / lines / loan deductions, payslips,
  installments, tax-ledger rows. Runs save quietly and write one `Ran` line named in `HrmRuns` (payroll calculation and
  finalization, salary payments, arrears, annual increment, pay revision, close day, attendance import, shift
  assignment, entitlements, leave year end, reviews, GPF interest); `HrmJobSource` reports their last lines on
  `GET /system/status`. Decisions get verbs (`HrmVerbs`). Identity and employment changes are written by `ChangeLog`
  with the reason (their saves are quiet so the reason line is the only one). Document and education scans write
  `Downloaded`. `GET /audit`, `GET /audit/kinds` (IAM_AUDIT.VIEW) and `POST /audit/exports` (HR / ATTENDANCE / PAYROLL
  .EXPORT) come from `MapAuditTrail`; `/me/payslips/{id}/printed` notes one's own print. The rows of the old
  `employee_change_log` were copied in by the `AuditTrail` migration; the table is no longer written. The seeder acts as
  "System · Startup".
- **Directory for Administration and Taxation.** `GET /directory/offices` → `{ offices: [{ id, code, name, parentId,
  typeName }] }` (active units, for the scope picker). `GET /directory/holidays?from&to` (anonymous) →
  `{ holidays: [{ date, name, holidayType }] }`: organization-wide, non-optional holidays, at most three years.

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
src/Tests/HRM.Domain.Tests/  xUnit, ~150 tests named after the rules
```

Application services: `HrLookup` (batch labels: names, placements, posts, units, subtrees, employees in a unit),
`ServiceRecordReader` / `ServiceRecordWriter` (posts held; appointments, moves with pay fixation, separations),
`HrActionApplier`, `PayService` (scales and pay records), `CodeIssuer` (EMP-###, POST-####), `FileUploads`,
`WorkCalendar` (an employee's working days: shift weekdays, gazetted and local holidays), `LeaveService` (days and
balances), `PayrollInputBuilder`, `PayrollEngine` and `PayslipBuilder` (payroll), `CurrentEmployee` (self-service),
`HrmSettings` (the configured rules in one place), `ChangeLog` (identity changes with their reason, into the audit trail),
`OfficeScope` / `EmployeeScope` (data scope), `Sensitive` (HR_SENSITIVE masking), `DutyGuard` (duty rules),
`HrmRuns` / `HrmVerbs` (audit words), `HrActionChecks` with
the domain's `HrActionRules` (what an action may do to whom), `PayrollImpactFinder` (finalized runs a backdated change
touches).

## How the main flows work

- **Effective dating everywhere.** Org units, posts, pay scales, pay records, shifts, salary rules and overrides have
  `effective_from / effective_to`. A change from a date closes the current row the day before and opens a new one.
  A correction edits the latest row in place. History is never overwritten.
- **Employees.** Registration is one request: master record, contacts, addresses, family and, optionally, the
  appointment. The appointment writes the service-history row, the regular assignment and the starting pay. Numbers
  are issued as `EMP-###` when left blank.
- **HR actions.** draft → pending → approved → applied. `HrActionRules` checks every kind against the employee's
  state and post at draft, submit and apply (`GET .../hr-actions/options` lists what is possible and why not;
  `.../preview` gives the consequence line). Maker-checker: whoever drafted or submitted may not approve (duty rule
  HR_ACTION), and nobody decides an action about themselves (OWN_RECORD). Reject needs remarks. An approved action dated in the
  future waits ("takes effect on") and cannot be applied early. Applying writes everything the action means (transfer,
  promotion with pay fixation and the grade check, LWOP as an approved leave-without-pay application, suspension,
  separation kinds, reinstatement only after termination / dismissal / removal / suspension) and returns the finalized
  payroll runs a backdated change touches. The service history is append-only.
- **Separation** closes what the person holds (posts, open tasks, pending leave and requests, active pay record) and
  returns a checklist (sign-in account, assets in custody, loans, GP Fund, settlement). A settlement linked to a
  payroll slip is locked.
- **Employee changes.** CNIC, date of birth, employment terms, the account link and pay changes need a reason and are
  written to the audit trail with it (`GET /employees/{id}/history`). Appointment needs a date of
  birth and an age of 18 to 60. A non-regular employee becomes Regular only through a Regularization action. Contract,
  ad hoc, project and deputation-in employees carry a term (`term_from / term_to`); the register's `termEndingWithinDays`
  and the bell list those ending.
- **Attendance.** Recorded, imported by employee number, or created by "close day". Close day marks everyone holding a
  post without a record as on leave, holiday, weekend or absent. Future dates are refused.
- **Holidays are marked, not planned** (`POST /holidays/mark`, ATTENDANCE.RUN): the day before, or after when nobody came
  in, From..To (at most 7 days, at most 7 days ahead) become holidays and everyone holding a post without a check-in is
  marked Holiday (a closed absence too; leave and days worked are kept). `POST /holidays/unmark` undoes a day. The
  holiday rows stay the calendar leave days, close day and Taxation read.
- **Leave.** Balance = entitlement + the signed `leave_ledger`. Days are the working days of the employee's calendar
  (a half day takes 0.5 off). Applying checks overlap and the days available (pending applications count against
  them). No one approves their own leave; reject needs a reason; cancelling an approved leave needs ATTENDANCE.APPROVE
  and a reason; the decider, date and reason are kept. Approving writes usage rows per calendar year and turns closed
  absences into leave. Cancelling reverses the ledger rows. An inactive leave type is offered to no one. The year-end
  is idempotent (a carry-forward or a lapse per balance) and opens placeholder entitlements that a later grant fills.
- **Performance.** Periods; reviews opened one by one or in bulk. The evaluator in bulk is the holder of the post's
  reporting post. draft → submitted → (countersigned, when a countersigning officer is named) → acknowledged →
  finalized. Everything is locked after submit; the employee sees the review only once submitted (and countersigned).
  Weights must total 100; the score is worked out with the `Hrm:Per*Weight` setting (goals only by default) and a
  final score that differs needs a reason.
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
  - Maker-checker: duty rules PAYROLL_APPROVE (not the preparer, the reviewer or anyone who entered an adjustment) and
    PAYROLL_PAY (the approver does not finalize or pay). Approval refuses figures older than a change to the pay inputs.
    Who calculated, finalized, paid and reversed (with the reason) is kept.
  - Calculation can be repeated and keeps hand-entered adjustments.
  - **Finalize** runs in one DB transaction. It writes the tax ledger, loan recoveries, GPF subscriptions and advance
    recoveries, then issues the frozen JSON payslips and flips the status. The ledgers must be written before the
    flip, because the lock triggers refuse them afterwards.
  - **Reverse** is refused once a payment went out (it must be recorded as returned first). It undoes the recoveries:
    a recovered installment is closed and **re-issued** as a new installment, because `installment_id` is unique in
    `payroll_loan_deduction`. GPF rows are offset with adjustments. A replacement run then references the reversed one.
  - Regular runs: one live regular run per month. Supplementary runs pay those on no other run of the month. Arrears,
    bonus and final-settlement runs are made of adjustments; the engine adds only the extra tax they cause.
  - **Arrears** (`POST /payroll-runs/{id}/arrears`, dry run first): a finalized past month is worked out again on
    today's records and the earnings difference per employee becomes an Arrears or Recovery adjustment on the run.
    Running it again replaces its own adjustments. Loans, GPF and tax of the past month are untouched.
  - Months: a day belongs to one month (overlap refused); an open month without runs can change dates or be deleted.
  - Tax: `employee_tax_opening` (income and tax before this payroll paid the person) counts in the year-to-date.
- **Payments.** Generated per finalized run into the primary bank account (snapshotted), or as cheque / cash with no
  account. Processed with the bank reference; failed or returned payments are re-generated. The bank advice lists
  transfers only, grouped by bank; `GET /payroll-runs/{id}/reconciliation` compares net pay with payments. A run is
  marked paid when every slip not held has a processed payment.
- **Loans and GP Fund.** Sanction lays out the installment schedule; the last installment absorbs the rounding. A GPF
  advance is paid out of the fund and its recoveries go back into it. Repayments outside payroll are kept in the
  append-only `loan_repayment` with the receipt number; waivers, cancellations and write-offs need a reason (who and
  when are kept). All are refused while a pending run is deducting the loan. A loan cannot start in a past month. GPF interest for a fiscal year is rate / 12 on
  each month-end balance, posted once per account.

## Attention (the bell)

`GET /attention` counts only queues the caller may act on (oldest item's date where it means something). HR.APPROVE:
`hrActions.toApprove` (not the caller's own while HR_ACTION is on), `requests.toDecide`, `documents.toVerify`,
`increments.overdue`, `reviews.toFinalize`. HR.EDIT: `hrActions.toApply`, `hrActions.takeEffectLater`,
`hrActions.myDrafts` (forMe), `tasks.overdue`, `documents.expiring`, `separations.unsettled`, `retirements.past`,
`retirements.due`, `contracts.ending`, `employees.noDateOfBirth`, `employees.unplaced`. ATTENDANCE.APPROVE: `leave.toDecide`. PAYROLL.EDIT
/ APPROVE / POST: `payroll.toReview`, `payroll.toApprove`, `payroll.toFinalize`, `payroll.toPay`. A linked employee:
`my.tasks`, `my.tasksOverdue`, `my.reviewsToAcknowledge`, `my.evaluationsToWrite`, `my.countersignings`. The frontend
maps each key to a filtered screen in `features/hrm/hrm.attention.ts`; keep the two in step.

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
- **Employment types** are a catalogue (`/employment-types`, seeded Regular and Contract by the `EmploymentTypes`
  migration). Each follows one `EmploymentBasis` (Regular, FixedTerm, Deputation, Project), fixed once employees, posts
  or salary rules use it; at most one active type per Regular and Deputation basis, which a Regularization / a
  Deputation (in) gives the person. The employee keeps the type id and a copy of its basis; every rule reads the basis.
- **Sanctioned vs contract posts**: a post whose type follows the Regular rules is sanctioned (regular staff and
  deputationists); one of a fixed-term type is a contract post (GDA's own approved strength) and takes fixed-term staff
  only (`PostKinds`, checked on every appointment and move).
- **Contingent orders** (`EngagementOrder`, `/employees/{id}/orders`, `/orders/{id}/cancel`): a fixed-term (contingent)
  person holds no post; each order gives an office, a designation, from-until, a Daily or Monthly wage and the letter (an
  uploaded document). A renewal is a new order; orders never overlap; the days between two are not paid. The person's
  term follows their latest order. Payroll (`PayrollCalculator.WagesOnly`, used when the person holds no post in the
  month) pays the rate x days (Daily) or rate x days / month days (Monthly), less leave without pay; no allowances, GP
  Fund or rule deductions, only loans and income tax. Attendance close-day and holidays, office scope, the register's
  office/designation filters and "unplaced" count the order covering the day. Cancelling is refused once a finalized
  run paid its days.
- **Configured lists** (`/qualification-levels` with a rank, `/family-relations`, `/employment-methods`,
  `/adjustment-types` with a direction): records keep the id; a used value is deactivated, not deleted. Adjustment types
  Arrears and Recovery have fixed ids (the arrears run writes them) and stay on.
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
- Migrations after the first: `PeopleRulesAndLeaveDecisions` (2026-10-20, raw SQL in `Data/Sql/PeopleRulesSql.cs`),
  `PayAuditRepaymentsAndPer` (2026-10-21, `Data/Sql/PayAuditSql.cs`), `AuditTrail` (2026-10-21: `audit_entry`, the change
  log copied in, `AuditTrailSql.Protect`) and `DutyRulesLoansAndLeave` (2026-10-22: loan status `pending`,
  `leave_application.created_by`). New enum labels are added there with
  `ALTER TYPE ... ADD VALUE` in the C# order, not through Npgsql's annotations (which would place them
  alphabetically); a label can never be dropped. Trigger SQL goes at the end of `Up` (its table must exist) and at
  the start of `Down`.

## Configuration

- `FileStorage:RootPath`: `storage` by default (gitignored). In compose it is the `hrm-files` volume.
- `Documents`: 25 MB per document; pdf, images and office files; photos jpg / png / webp up to 5 MB.
- `Hrm`: employee number prefix `EMP` with 3 digits, post code prefix `POST` with 4 digits, retirement age 60, annual
  increment on 1 December. Rules GDA still has to confirm keep the old behaviour by default:
  `IncrementQualifyingMonths` 0, `RetireOnDayBeforeBirthday` false, `PerGoalWeight / PerKpiWeight /
  PerCompetencyWeight` 100 / 0 / 0. `GET /settings` shows the frontend what is in force, with the duty rules.
- Maker-checker is no longer configuration: the duty rules (Identity, Administration › Configuration) decide it.

## Testing

`dotnet test src/ERP.slnx` runs `src/Tests/HRM.Domain.Tests` (149 tests). The API flows are in the frontend repo:
`node tools/e2e/hrm-api.e2e.mjs` (77 steps; it cleans up after itself). Second-person steps (approvals, sanctions) and
the permission, sensitive-field and office-scope checks use the permanent accounts `e2e.hrm.maker` / `e2e.hrm.checker`
(`tools/e2e/accounts.mjs`, whose access the suite sets); self-service links `e2e.hrm.maker` to the Appraisee and unlinks it. `Fixture.cs` builds employees, components, rules, a
tax year (FY 2025-26 salaried slabs) and loans. Add a test for every new domain rule, named after the rule, e.g.
`Net_pay_never_goes_below_zero_the_least_important_loan_is_left_for_later`.

## Gotchas

- `employee.full_name` is generated by Postgres: never assign it. Use `DisplayName` on an unsaved employee.
- Inside LINQ query syntax, `from` is a keyword: don't name locals `from` / `to` in methods that use `from x in ...`.
- `FirstOrDefault()` on a list of value tuples returns a default tuple, not null, so `is { Field: var f }` always
  matches. Check `Count` or use a nullable projection.
- Employment statuses are grouped in `Employee.EmploymentStatuses` (InService, LeftService, Reinstatable): use them
  instead of listing statuses by hand.
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
