using Microsoft.EntityFrameworkCore.Migrations;

/// The parts of docs/gda_hrms_schema.sql that EF cannot express: exclusion constraints, functions, triggers, views and
/// comments. Called from the InitialHrmsSchema migration after EF has created the tables.
///
/// Differences from the SQL file, on purpose:
/// - Every EXCLUDE constraint is DEFERRABLE INITIALLY DEFERRED, so closing one period and opening the next in the same
///   save is checked at commit (EF does not promise the order of the two statements).
/// - uq_employee_contact_primary, uq_eba_one_primary and uq_edu_one_highest are deferrable exclusion constraints with
///   the same name and meaning as the partial unique indexes, for the same reason (moving the flag between rows).
/// - The post-capacity check is a deferred constraint trigger that also locks the post row, so two concurrent
///   assignments cannot both take the last seat.
/// - The business triggers (run status workflow, loan posting on finalize, task progress, payment snapshot, request and
///   goal-weight checks) are domain code, not triggers. The guard triggers stay: append-only ledgers, updated_at, a
///   finalized run's rows locked, the payslip snapshot frozen.
/// - The access roles of the security section are a DBA step, not a migration (run section 3 of the SQL file).
///
/// Never edit this file after its migration has shipped: a fresh database would then get different SQL than existing
/// ones. Put later changes in a new class called from a new migration.
public static class HrmsSchemaSql
{
  public static void Up(MigrationBuilder migrationBuilder)
  {
    foreach (var statement in Statements)
      migrationBuilder.Sql(statement);
  }

  public static void Down(MigrationBuilder migrationBuilder)
  {
    foreach (var statement in DownStatements)
      migrationBuilder.Sql(statement);
  }

  private static readonly string[] Statements =
  [
    // ---- helper functions ----
    """
    CREATE OR REPLACE FUNCTION hrms.fn_set_updated_at() RETURNS trigger
    LANGUAGE plpgsql AS $$
    BEGIN
      NEW.updated_at := now();
      RETURN NEW;
    END $$;
    """,
    """
    CREATE OR REPLACE FUNCTION hrms.fn_block_mutation() RETURNS trigger
    LANGUAGE plpgsql AS $$
    BEGIN
      RAISE EXCEPTION '% is append-only: % is not allowed (insert an offsetting row instead)', TG_TABLE_NAME, TG_OP;
    END $$;
    """,

    // ---- no overlapping periods (deferred to commit) ----
    """
    ALTER TABLE hrms.organization_unit_version ADD CONSTRAINT ex_ouv_no_overlap
      EXCLUDE USING gist (org_unit_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.pay_scale_version ADD CONSTRAINT ex_psv_no_overlap
      EXCLUDE USING gist (grade_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      WHERE (status = 'active') DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.post_version ADD CONSTRAINT ex_pv_no_overlap
      EXCLUDE USING gist (post_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.employee_pay_record ADD CONSTRAINT ex_epr_no_overlap
      EXCLUDE USING gist (employee_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.position_assignment ADD CONSTRAINT ex_pa_one_regular
      EXCLUDE USING gist (employee_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      WHERE (assignment_type = 'regular' AND status = 'active') DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.performance_period ADD CONSTRAINT ex_pp_no_overlap
      EXCLUDE USING gist (daterange(start_date, end_date, '[]') WITH &&)
      WHERE (status = 'active') DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.employee_shift ADD CONSTRAINT ex_es_no_overlap
      EXCLUDE USING gist (employee_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.leave_application ADD CONSTRAINT ex_la_no_overlap
      EXCLUDE USING gist (employee_id WITH =, daterange(start_date, end_date, '[]') WITH &&)
      WHERE (status IN ('pending', 'approved')) DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.employee_salary_component ADD CONSTRAINT ex_esc_no_overlap
      EXCLUDE USING gist (employee_id WITH =, salary_component_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.tax_year ADD CONSTRAINT ex_ty_no_overlap
      EXCLUDE USING gist (daterange(start_date, end_date, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.tax_slab ADD CONSTRAINT ex_slab_no_overlap
      EXCLUDE USING gist (tax_year_id WITH =, numrange(min_income, max_income, '[)') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.gp_fund_interest_rate ADD CONSTRAINT ex_gir_no_overlap
      EXCLUDE USING gist (daterange(effective_from, effective_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.payroll_transaction_segment ADD CONSTRAINT ex_pts_no_overlap
      EXCLUDE USING gist (payroll_transaction_id WITH =, daterange(period_from, period_to, '[]') WITH &&)
      DEFERRABLE INITIALLY DEFERRED;
    """,

    // ---- "one per employee" flags that move between rows (deferred to commit) ----
    """
    ALTER TABLE hrms.employee_contact ADD CONSTRAINT uq_employee_contact_primary
      EXCLUDE USING btree (employee_id WITH =, contact_type WITH =) WHERE (is_primary) DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.employee_bank_account ADD CONSTRAINT uq_eba_one_primary
      EXCLUDE USING btree (employee_id WITH =) WHERE (is_primary AND status = 'active') DEFERRABLE INITIALLY DEFERRED;
    """,
    """
    ALTER TABLE hrms.employee_education ADD CONSTRAINT uq_edu_one_highest
      EXCLUDE USING btree (employee_id WITH =) WHERE (is_highest_qualification) DEFERRABLE INITIALLY DEFERRED;
    """,

    // ---- post capacity: at commit, with the post row locked ----
    """
    CREATE OR REPLACE FUNCTION hrms.fn_check_post_capacity() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE
      v_seats int; v_life hrms.post_lifecycle_enum; v_taken int; v_row hrms.position_assignment%ROWTYPE;
    BEGIN
      -- the row as it is at commit (it may have changed or gone since the statement that queued this check)
      SELECT * INTO v_row FROM hrms.position_assignment WHERE id = NEW.id;
      IF NOT FOUND OR v_row.assignment_type <> 'regular' OR v_row.status <> 'active' THEN
        RETURN NULL;
      END IF;

      -- serialise assignments to the same post so two transactions cannot both take the last seat
      PERFORM 1 FROM hrms.post WHERE id = v_row.post_id FOR UPDATE;

      SELECT sanctioned_count, lifecycle_status INTO v_seats, v_life
        FROM hrms.post_version
       WHERE post_id = v_row.post_id
         AND v_row.effective_from BETWEEN effective_from AND coalesce(effective_to, 'infinity'::date);
      IF NOT FOUND THEN
        RAISE EXCEPTION 'post % has no post_version in effect on %', v_row.post_id, v_row.effective_from;
      END IF;
      IF v_life <> 'sanctioned' THEN
        RAISE EXCEPTION 'post % is % and cannot receive a regular assignment', v_row.post_id, v_life;
      END IF;

      SELECT count(*) INTO v_taken
        FROM hrms.position_assignment pa
       WHERE pa.post_id = v_row.post_id
         AND pa.assignment_type = 'regular' AND pa.status = 'active'
         AND pa.id <> v_row.id
         AND daterange(pa.effective_from, pa.effective_to, '[]') && daterange(v_row.effective_from, v_row.effective_to, '[]');
      IF v_taken >= v_seats THEN
        RAISE EXCEPTION 'post % is full: % of % seats already held in the requested period', v_row.post_id, v_taken, v_seats;
      END IF;
      RETURN NULL;
    END $$;
    """,
    """
    CREATE CONSTRAINT TRIGGER trg_pa_capacity
      AFTER INSERT OR UPDATE ON hrms.position_assignment
      DEFERRABLE INITIALLY DEFERRED
      FOR EACH ROW EXECUTE FUNCTION hrms.fn_check_post_capacity();
    """,

    // ---- append-only ledgers ----
    "CREATE TRIGGER trg_esh_append_only BEFORE UPDATE OR DELETE ON hrms.employee_service_history FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();",
    "CREATE TRIGGER trg_leave_ledger_append_only BEFORE UPDATE OR DELETE ON hrms.leave_ledger FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();",
    "CREATE TRIGGER trg_gpt_append_only BEFORE UPDATE OR DELETE ON hrms.gp_fund_transaction FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();",
    "CREATE TRIGGER trg_etu_append_only BEFORE UPDATE OR DELETE ON hrms.employee_task_update FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();",

    // ---- a finalized / paid / reversed run's figures are immutable ----
    """
    CREATE OR REPLACE FUNCTION hrms.fn_block_if_run_locked() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE v_run uuid; v_status hrms.payroll_run_status_enum; r record;
    BEGIN
      r := CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;

      IF TG_TABLE_NAME = 'payroll_transaction' THEN
        v_run := r.payroll_run_id;
      ELSE
        SELECT payroll_run_id INTO v_run FROM hrms.payroll_transaction WHERE id = r.payroll_transaction_id;
      END IF;

      SELECT status INTO v_status FROM hrms.payroll_run WHERE id = v_run;

      -- payroll_transaction.status / remarks (hold / release) may still change after finalization
      IF TG_TABLE_NAME = 'payroll_transaction' AND TG_OP = 'UPDATE' AND v_status IN ('finalized','paid') THEN
        IF NEW.gross_pay = OLD.gross_pay AND NEW.total_deductions = OLD.total_deductions
           AND NEW.net_payable = OLD.net_payable AND NEW.days_payable IS NOT DISTINCT FROM OLD.days_payable
           AND NEW.employee_id = OLD.employee_id AND NEW.payroll_run_id = OLD.payroll_run_id THEN
          RETURN NEW;
        END IF;
      END IF;

      IF v_status IN ('finalized','paid','reversed') THEN
        RAISE EXCEPTION '% is locked: payroll_run % is %', TG_TABLE_NAME, v_run, v_status;
      END IF;
      RETURN r;
    END $$;
    """,
    "CREATE TRIGGER trg_lock_pt BEFORE INSERT OR UPDATE OR DELETE ON hrms.payroll_transaction FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",
    "CREATE TRIGGER trg_lock_pts BEFORE INSERT OR UPDATE OR DELETE ON hrms.payroll_transaction_segment FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",
    "CREATE TRIGGER trg_lock_pcd BEFORE INSERT OR UPDATE OR DELETE ON hrms.payroll_component_detail FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",
    "CREATE TRIGGER trg_lock_padj BEFORE INSERT OR UPDATE OR DELETE ON hrms.payroll_adjustment FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",
    "CREATE TRIGGER trg_lock_pld BEFORE INSERT OR UPDATE OR DELETE ON hrms.payroll_loan_deduction FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",
    "CREATE TRIGGER trg_lock_etl BEFORE INSERT OR UPDATE OR DELETE ON hrms.employee_tax_ledger FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_if_run_locked();",

    // ---- the payslip snapshot never changes (file_reference may be filled in later) ----
    """
    CREATE OR REPLACE FUNCTION hrms.fn_payslip_guard() RETURNS trigger
    LANGUAGE plpgsql AS $$
    BEGIN
      IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'payslip rows cannot be deleted';
      ELSIF NEW.snapshot_json IS DISTINCT FROM OLD.snapshot_json
         OR NEW.payroll_transaction_id <> OLD.payroll_transaction_id THEN
        RAISE EXCEPTION 'payslip snapshot is immutable';
      END IF;
      RETURN NEW;
    END $$;
    """,
    "CREATE TRIGGER trg_payslip_guard BEFORE UPDATE OR DELETE ON hrms.payslip FOR EACH ROW EXECUTE FUNCTION hrms.fn_payslip_guard();",

    // ---- payable days (for reports and hand-written SQL; the payroll engine works it out the same way) ----
    """
    CREATE OR REPLACE FUNCTION hrms.fn_days_payable(p_employee uuid, p_from date, p_to date, p_join date DEFAULT NULL, p_leave date DEFAULT NULL)
    RETURNS numeric LANGUAGE sql STABLE AS $$
      WITH win AS (
        SELECT greatest(p_from, coalesce(p_join, p_from)) AS d1,
               least(p_to,   coalesce(p_leave, p_to))     AS d2
      ), unpaid AS (
        SELECT coalesce(sum(
                 greatest(0, least(la.end_date, w.d2) - greatest(la.start_date, w.d1) + 1)
               ), 0) AS d
          FROM win w
          JOIN hrms.leave_application la ON la.employee_id = p_employee AND la.status = 'approved'
          JOIN hrms.leave_type lt ON lt.id = la.leave_type_id AND lt.affects_payroll
         WHERE la.start_date <= w.d2 AND la.end_date >= w.d1
      )
      SELECT greatest(0, (w.d2 - w.d1 + 1) - u.d)::numeric FROM win w, unpaid u;
    $$;
    """,

    // ---- views ----
    """
    CREATE VIEW hrms.v_organization_unit_current AS
    WITH RECURSIVE cur AS (
      SELECT v.*, u.code
        FROM hrms.organization_unit_version v
        JOIN hrms.organization_unit u ON u.id = v.org_unit_id
       WHERE v.status = 'active'
         AND current_date BETWEEN v.effective_from AND coalesce(v.effective_to, 'infinity'::date)
    ), tree AS (
      SELECT c.org_unit_id, c.parent_unit_id, 1 AS level, ARRAY[c.org_unit_id] AS path
        FROM cur c WHERE c.parent_unit_id IS NULL
      UNION ALL
      SELECT c.org_unit_id, c.parent_unit_id, t.level + 1, t.path || c.org_unit_id
        FROM cur c JOIN tree t ON c.parent_unit_id = t.org_unit_id
       WHERE NOT c.org_unit_id = ANY (t.path)
    )
    SELECT c.org_unit_id, c.code, c.name, c.unit_type_id, c.parent_unit_id, c.location_id,
           c.head_post_id, t.level, c.effective_from
      FROM cur c LEFT JOIN tree t USING (org_unit_id);
    """,
    """
    CREATE VIEW hrms.v_post_occupancy AS
    SELECT pv.post_id, p.post_code, pv.designation_id, pv.grade_id, pv.org_unit_id,
           pv.sanctioned_count,
           coalesce(h.cnt, 0) AS filled_count,
           CASE
             WHEN pv.lifecycle_status = 'abolished' THEN 'abolished'::hrms.position_status_enum
             WHEN pv.lifecycle_status = 'frozen'    THEN 'frozen'::hrms.position_status_enum
             WHEN coalesce(h.cnt,0) = 0                  THEN 'vacant'::hrms.position_status_enum
             WHEN coalesce(h.cnt,0) < pv.sanctioned_count THEN 'partially_filled'::hrms.position_status_enum
             ELSE 'filled'::hrms.position_status_enum
           END AS status
      FROM hrms.post_version pv
      JOIN hrms.post p ON p.id = pv.post_id
      LEFT JOIN LATERAL (
        SELECT count(*) AS cnt
          FROM hrms.position_assignment pa
         WHERE pa.post_id = pv.post_id AND pa.assignment_type = 'regular' AND pa.status = 'active'
           AND current_date BETWEEN pa.effective_from AND coalesce(pa.effective_to, 'infinity'::date)
      ) h ON true
     WHERE current_date BETWEEN pv.effective_from AND coalesce(pv.effective_to, 'infinity'::date);
    """,
    """
    CREATE VIEW hrms.v_leave_balance AS
    SELECT e.employee_id, e.leave_type_id, e.year,
           e.entitled_days,
           coalesce(sum(l.days) FILTER (WHERE l.txn_type IN ('opening','accrual','carry_forward')), 0)  AS accrued_days,
           -coalesce(sum(l.days) FILTER (WHERE l.txn_type IN ('usage','usage_reversal')), 0)            AS used_days,
           e.entitled_days + coalesce(sum(l.days), 0)                                                    AS balance_days
      FROM hrms.leave_entitlement e
      LEFT JOIN hrms.leave_ledger l
             ON l.employee_id = e.employee_id AND l.leave_type_id = e.leave_type_id AND l.year = e.year
     GROUP BY e.employee_id, e.leave_type_id, e.year, e.entitled_days;
    """,
    "COMMENT ON VIEW hrms.v_leave_balance IS 'balance_days = entitled_days + signed ledger total. If you credit entitlement via ledger \"accrual\" rows instead, set entitled_days = 0.';",
    """
    CREATE VIEW hrms.v_gp_fund_balance AS
    SELECT a.id AS gp_fund_account_id, a.employee_id,
           coalesce(sum(t.amount), 0)                                          AS balance,
           coalesce(sum(t.amount) FILTER (WHERE t.txn_type = 'subscription'), 0) AS total_subscribed,
           coalesce(sum(t.amount) FILTER (WHERE t.txn_type = 'interest'), 0)     AS total_interest
      FROM hrms.gp_fund_account a
      LEFT JOIN hrms.gp_fund_transaction t ON t.gp_fund_account_id = a.id
     GROUP BY a.id, a.employee_id;
    """,
    """
    CREATE VIEW hrms.v_employee_tax_ytd AS
    SELECT l.employee_id, l.tax_year_id,
           sum(l.taxable_income) AS taxable_income_ytd,
           sum(l.tax_withheld)   AS tax_withheld_ytd
      FROM hrms.employee_tax_ledger l
      JOIN hrms.payroll_transaction t ON t.id = l.payroll_transaction_id
      JOIN hrms.payroll_run r ON r.id = t.payroll_run_id
     WHERE r.status <> 'reversed'
     GROUP BY l.employee_id, l.tax_year_id;
    """,

    // ---- updated_at kept current on every table that has the column ----
    """
    DO $$
    DECLARE t record;
    BEGIN
      FOR t IN
        SELECT c.table_name
          FROM information_schema.columns c
          JOIN information_schema.tables tb
            ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name AND tb.table_type = 'BASE TABLE'
         WHERE c.table_schema = 'hrms' AND c.column_name = 'updated_at'
      LOOP
        EXECUTE format(
          'CREATE TRIGGER trg_%I_updated_at BEFORE UPDATE ON hrms.%I FOR EACH ROW EXECUTE FUNCTION hrms.fn_set_updated_at()',
          t.table_name, t.table_name);
      END LOOP;
    END $$;
    """,

    // ---- the schema file's comments ----
    "COMMENT ON TABLE hrms.organization_unit_type IS 'Configurable catalogue of unit types (Department / Wing / Section / Office / Division). hierarchy_level lives ONLY here; a unit''s depth is derived from its parent chain.';",
    "COMMENT ON TABLE hrms.organization_unit IS 'Immutable identity of an org unit. Name, type, parent, location and head live in organization_unit_version so restructuring never destroys history (same pattern as post / post_version).';",
    "COMMENT ON TABLE hrms.designation IS 'Pure job-title catalogue. Independent of BPS, org unit and headcount.';",
    "COMMENT ON TABLE hrms.pay_scale_version IS 'Versioned pay-scale revisions: a new notification creates a new row. The per-year increment amount is NOT stored here (it was redundant); pay_scale_stage is the single source of truth for stage amounts.';",
    "COMMENT ON TABLE hrms.pay_scale_stage IS 'Annual-increment stage within a pay-scale version, e.g. BPS-17 Stage 4 basic pay.';",
    "COMMENT ON TABLE hrms.post IS 'Immutable identity of a sanctioned post. Mutable attributes live in post_version.';",
    "COMMENT ON TABLE hrms.post_version IS 'One version of a post''s attributes. sanctioned_count = number of seats this post identity carries (1 = a single seat; >1 = a pool such as \"10 Drivers\"). Whether those seats are vacant/filled is DERIVED in v_post_occupancy from position_assignment - it is no longer stored, so it cannot drift.';",
    "COMMENT ON TABLE hrms.employee IS 'Core identity only. full_name is a generated column. CNIC is stored in plain text (needed for uniqueness/search): protect with column privileges (see security section) and disk/backup encryption.';",
    "COMMENT ON COLUMN hrms.employee.cnic IS 'Accepts 13 digits with or without dashes (35202-1234567-1). Normalise in the application so the UNIQUE index is meaningful.';",
    "COMMENT ON TABLE hrms.employee_pay_record IS 'Effective-dated pay position of an employee (stage + actual basic pay). Annual increments close the current row and open the next stage. Payroll reads Basic Pay from here.';",
    "COMMENT ON TABLE hrms.employee_bank_account IS 'Restricted-access table; not joined into general reporting views. Payment rows snapshot these values at payment time.';",
    "COMMENT ON TABLE hrms.recruitment_method IS 'Project Regularization / Board of Authority / Public Service (CSS, PMS ...). Referenced from employee_service_history.';",
    "COMMENT ON TABLE hrms.employee_service_history IS 'Append-only service ledger: ONE row per service event (appointment, transfer, promotion, deputation ...). It records WHAT happened and why. The resulting occupancy of a post is recorded in position_assignment (the two tables complement each other). UPDATE/DELETE are blocked by trigger; corrections are offsetting rows.';",
    "COMMENT ON TABLE hrms.position_assignment IS 'Employee occupancy of a sanctioned post. Acting / additional-charge assignments can coexist with a regular one. A trigger stops regular assignments exceeding the post''s sanctioned_count and refuses frozen / abolished posts.';",
    "COMMENT ON TABLE hrms.hr_action_request IS 'Draft/pending HR change. Routed through the platform approval workflow (approval_request_id is a plain uuid pointer). When applied, an employee_service_history row is written and linked in resulting_service_history_id.';",
    "COMMENT ON TABLE hrms.leave_ledger IS 'Append-only leave transactions (accrual, usage, carry-forward, lapse, encashment, adjustment). Balances are derived, so they can always be audited.';",
    "COMMENT ON TABLE hrms.salary_component IS 'Pay components from the Officers'' pay slip. EARNING: Basic Pay, GDA Authority Allowance, Medical Allowance, Housing Subsidy, Utility Allowance, Adhoc Relief Allowance (one row per notification year). DEDUCTION: BF, GP Fund, MCA, GPF Advance, RB&D, Income Tax.';",
    "COMMENT ON TABLE hrms.salary_component_rule IS 'Versioned, scoped rules. Several rules of one component may overlap in time when their scope differs (designation / BPS range / org unit / employment type); priority breaks ties, so no time-exclusion is enforced here. Each Adhoc Relief year is its own component.';",
    "COMMENT ON TABLE hrms.employee_salary_component IS 'Employee-specific overrides of the standard rule. Exactly one of fixed amount / percentage.';",
    "COMMENT ON TABLE hrms.loan_type IS 'e.g. Motor Car Advance (MCA), GP Fund Advance. salary_component_id says which pay-slip deduction line carries the installment, so the loan is never double counted.';",
    "COMMENT ON TABLE hrms.payroll_run IS 'One live regular run per period (partial unique index); extra runs use run_type. Locked once finalized: child rows are protected by triggers. Status moves only along the allowed workflow.';",
    "COMMENT ON TABLE hrms.payroll_transaction IS 'Pay-slip header: one row per employee per run. Post / grade changes inside the month are carried by payroll_transaction_segment (so proration across two posts is possible).';",
    "COMMENT ON TABLE hrms.payroll_transaction_segment IS 'One row per post/grade/stage the employee held within the pay period (mid-month transfer, promotion, acting charge ...).';",
    "COMMENT ON TABLE hrms.payroll_component_detail IS 'One pay-slip line. Loan / advance deductions (MCA, GPF Adv) appear here ONCE with source=''loan'' and a link to payroll_loan_deduction, which links to the exact installment - so no double counting and full reconciliation.';",
    "COMMENT ON TABLE hrms.payroll_payment IS 'Restricted-access (holds bank details). Destination snapshot is copied from the account when the payment is created and never changes; composite FK guarantees the account belongs to the paid employee.';",
    "COMMENT ON TABLE hrms.employee_task IS 'Lightweight task assignment for the Employee Portal. progress_percentage mirrors the latest employee_task_update (kept by the application).';"
  ];

  private static readonly string[] DownStatements =
  [
    "DROP VIEW IF EXISTS hrms.v_employee_tax_ytd;",
    "DROP VIEW IF EXISTS hrms.v_gp_fund_balance;",
    "DROP VIEW IF EXISTS hrms.v_leave_balance;",
    "DROP VIEW IF EXISTS hrms.v_post_occupancy;",
    "DROP VIEW IF EXISTS hrms.v_organization_unit_current;",
    "DROP FUNCTION IF EXISTS hrms.fn_days_payable(uuid, date, date, date, date);",
    "DROP FUNCTION IF EXISTS hrms.fn_payslip_guard() CASCADE;",
    "DROP FUNCTION IF EXISTS hrms.fn_block_if_run_locked() CASCADE;",
    "DROP FUNCTION IF EXISTS hrms.fn_check_post_capacity() CASCADE;",
    "DROP FUNCTION IF EXISTS hrms.fn_block_mutation() CASCADE;",
    "DROP FUNCTION IF EXISTS hrms.fn_set_updated_at() CASCADE;"
  ];
}
