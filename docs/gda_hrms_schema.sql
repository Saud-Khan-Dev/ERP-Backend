-- =====================================================================
-- GDA HRMS ERP  -  PostgreSQL schema (revised)
-- Galiyat Development Authority, Government of Khyber Pakhtunkhwa
--
-- Requires PostgreSQL 15+ (tested on 16.15). Run as a role that can
-- CREATE EXTENSION (btree_gist, pgcrypto are in postgresql-contrib).
--
-- Conventions
--   * everything lives in schema "hrms"
--   * every id is uuid default gen_random_uuid()
--   * money is numeric(14,2)
--   * *_by columns (created_by, approved_by, verified_by, reviewed_by,
--     assigned_by, updated_by) are ALWAYS platform users.id (plain uuid,
--     no cross-schema FK).  Columns named *_employee_id point to employee.
--   * effective-dated rows use closed-open logic via daterange(from,to,'[]')
--     and are protected by EXCLUDE constraints against overlap.
--
-- How the HRM service builds this schema (EF migration InitialHrmsSchema +
-- src/Services/HRM/HRM.Infrastructure/Data/Sql/HrmsSchemaSql.cs). Tables,
-- columns, types, defaults, CHECKs, FKs, indexes, views and seed-free
-- catalogue structure match this file, with these deliberate differences:
--   * every EXCLUDE constraint is DEFERRABLE INITIALLY DEFERRED;
--   * uq_employee_contact_primary, uq_eba_one_primary, uq_edu_one_highest
--     are deferrable btree EXCLUDE constraints (same names and meaning)
--     instead of partial unique indexes;
--   * fn_check_post_capacity runs as a DEFERRABLE INITIALLY DEFERRED
--     constraint trigger and locks the post row;
--   * the business triggers are application code instead:
--     fn_check_goal_weights, fn_payment_prepare, fn_payroll_run_transition,
--     fn_payroll_apply_loans, fn_task_mirror_progress, fn_request_validate.
--     The guard triggers are kept: fn_set_updated_at, fn_block_mutation,
--     fn_block_if_run_locked (a slip's hold / release may change after
--     finalization), fn_payslip_guard; fn_days_payable is kept;
--   * UNIQUE (...) constraints are unique indexes with the same names;
--   * section 3 of FINISHING (access roles and grants) is not run by the
--     migration: it is a DBA step;
--   * section 4 (seeds) is replaced by the service's own seeder.
-- =====================================================================

CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS hrms;
SET search_path = hrms, public;

-- ---------------------------------------------------------------------
-- ENUMS
-- ---------------------------------------------------------------------
CREATE TYPE gender_enum            AS ENUM ('male','female','other');
CREATE TYPE marital_status_enum    AS ENUM ('single','married','divorced','widowed');
CREATE TYPE employment_status_enum AS ENUM ('active','on_leave','suspended','deputed_out','retired','resigned','terminated','deceased');

-- regular = Permanent (see employment_method), deputation_in = Permanent deputed in from
-- another department, contract = Contingent, project_based = Project-Based, daily_wage = Daily Wages
CREATE TYPE employment_type_enum   AS ENUM ('regular','contract','project_based','deputation_in','adhoc','daily_wage');
CREATE TYPE employment_method_enum AS ENUM ('scheduled_seat','gda_personal');

-- post lifecycle is stored; occupancy (vacant / partially_filled / filled) is DERIVED in v_post_occupancy
CREATE TYPE post_lifecycle_enum    AS ENUM ('sanctioned','frozen','abolished');
CREATE TYPE position_status_enum   AS ENUM ('sanctioned','vacant','partially_filled','filled','abolished','frozen');
CREATE TYPE assignment_type_enum   AS ENUM ('regular','acting','additional_charge','look_after');

CREATE TYPE component_type_enum    AS ENUM ('earning','deduction');
CREATE TYPE calculation_method_enum AS ENUM ('fixed','percentage','formula','tiered');
CREATE TYPE component_source_enum  AS ENUM ('rule','override','loan','gpf','tax','adjustment','manual');

CREATE TYPE payroll_run_status_enum    AS ENUM ('draft','calculated','reviewed','approved','finalized','paid','reversed');
CREATE TYPE payroll_run_type_enum      AS ENUM ('regular','supplementary','arrears','bonus','final_settlement');
CREATE TYPE payroll_period_status_enum AS ENUM ('open','closed','locked');
CREATE TYPE payroll_txn_status_enum    AS ENUM ('calculated','held','released');
CREATE TYPE adjustment_type_enum       AS ENUM ('arrears','recovery','bonus','correction','other');
CREATE TYPE payment_status_enum        AS ENUM ('pending','processed','failed','returned');
CREATE TYPE payment_method_enum        AS ENUM ('bank_transfer','cheque','cash');

CREATE TYPE verification_status_enum AS ENUM ('unverified','pending','verified','rejected');
CREATE TYPE record_status_enum       AS ENUM ('active','inactive');

CREATE TYPE task_priority_enum AS ENUM ('low','medium','high','urgent');
CREATE TYPE task_status_enum   AS ENUM ('pending','in_progress','completed','on_hold','cancelled');
CREATE TYPE employee_request_status_enum AS ENUM ('pending','approved','rejected','cancelled');

CREATE TYPE contact_type_enum  AS ENUM ('mobile','phone','email','whatsapp','other');
CREATE TYPE address_type_enum  AS ENUM ('permanent','current','mailing','other');
CREATE TYPE qualification_level_enum AS ENUM ('matric','intermediate','bachelors','masters','mphil','phd','other');
CREATE TYPE separation_type_enum AS ENUM ('retirement','resignation','termination','dismissal','death','end_of_contract','deputation_end','other');

CREATE TYPE hr_action_type_enum   AS ENUM ('appointment','joining','transfer','promotion','demotion','deputation_in','deputation_out','regularization','lwop','suspension','reinstatement','retirement','resignation','termination','death','other');
CREATE TYPE hr_action_status_enum AS ENUM ('draft','pending','approved','rejected','cancelled','applied');

CREATE TYPE review_status_enum AS ENUM ('draft','submitted','acknowledged','finalized');
CREATE TYPE attendance_status_enum AS ENUM ('present','absent','late','half_day','on_leave','official_duty','holiday','weekend');
CREATE TYPE holiday_type_enum  AS ENUM ('public','religious','provincial','optional');

CREATE TYPE leave_status_enum   AS ENUM ('pending','approved','rejected','cancelled');
CREATE TYPE leave_txn_type_enum AS ENUM ('opening','accrual','usage','usage_reversal','carry_forward','lapse','encashment','adjustment');

CREATE TYPE loan_status_enum        AS ENUM ('active','completed','cancelled','written_off');
CREATE TYPE installment_status_enum AS ENUM ('pending','partial','paid','waived');
CREATE TYPE gpf_txn_type_enum       AS ENUM ('opening','subscription','interest','advance','advance_recovery','withdrawal','final_payment','adjustment');

-- ---------------------------------------------------------------------
-- HELPER FUNCTIONS
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END $$;

-- Blocks UPDATE/DELETE on append-only ledgers (corrections = offsetting rows)
CREATE OR REPLACE FUNCTION fn_block_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION '% is append-only: % is not allowed (insert an offsetting row instead)', TG_TABLE_NAME, TG_OP;
END $$;
-- =====================================================================
-- MODULE 1: ORGANIZATION HIERARCHY
-- =====================================================================
CREATE TABLE organization_unit_type (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name            varchar NOT NULL UNIQUE,
  code            varchar NOT NULL UNIQUE,
  hierarchy_level int CHECK (hierarchy_level IS NULL OR hierarchy_level >= 0),
  is_active       boolean NOT NULL DEFAULT true
);
COMMENT ON TABLE organization_unit_type IS
  'Configurable catalogue of unit types (Department / Wing / Section / Office / Division). hierarchy_level lives ONLY here; a unit''s depth is derived from its parent chain.';

CREATE TABLE location (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name         varchar NOT NULL,
  address_line varchar,
  city         varchar,
  district     varchar,
  province     varchar DEFAULT 'Khyber Pakhtunkhwa',
  is_active    boolean NOT NULL DEFAULT true
);

-- Identity of an organisation unit (never changes) ...
CREATE TABLE organization_unit (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  code       varchar NOT NULL UNIQUE,
  created_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid
);
COMMENT ON TABLE organization_unit IS
  'Immutable identity of an org unit. Name, type, parent, location and head live in organization_unit_version so restructuring never destroys history (same pattern as post / post_version).';

-- ... and its effective-dated attributes
CREATE TABLE organization_unit_version (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  org_unit_id    uuid NOT NULL REFERENCES organization_unit(id),
  unit_type_id   uuid NOT NULL REFERENCES organization_unit_type(id),
  parent_unit_id uuid REFERENCES organization_unit(id),
  name           varchar NOT NULL,
  location_id    uuid REFERENCES location(id),
  head_post_id   uuid,                       -- FK added after post is created
  effective_from date NOT NULL,
  effective_to   date,
  status         record_status_enum NOT NULL DEFAULT 'active',
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     uuid,
  updated_at     timestamptz,
  updated_by     uuid,
  CONSTRAINT ck_ouv_dates   CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ck_ouv_no_self CHECK (parent_unit_id IS NULL OR parent_unit_id <> org_unit_id),
  CONSTRAINT ex_ouv_no_overlap EXCLUDE USING gist (
    org_unit_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  )
);
CREATE INDEX idx_ouv_parent ON organization_unit_version (parent_unit_id);

-- =====================================================================
-- MODULE 2: DESIGNATION & PAY SCALE
-- =====================================================================
CREATE TABLE designation (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  title       varchar NOT NULL UNIQUE,
  code        varchar UNIQUE,
  description text,
  is_active   boolean NOT NULL DEFAULT true
);
COMMENT ON TABLE designation IS 'Pure job-title catalogue. Independent of BPS, org unit and headcount.';

CREATE TABLE pay_scale_grade (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  bps_number int NOT NULL UNIQUE CHECK (bps_number BETWEEN 1 AND 22),
  grade_name varchar,
  is_active  boolean NOT NULL DEFAULT true
);

CREATE TABLE pay_scale_version (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  grade_id         uuid NOT NULL REFERENCES pay_scale_grade(id),
  min_basic_pay    numeric(14,2) NOT NULL CHECK (min_basic_pay >= 0),
  max_basic_pay    numeric(14,2) NOT NULL,
  increment_rule   text,
  version_label    varchar,
  notification_ref varchar,
  effective_from   date NOT NULL,
  effective_to     date,
  status           record_status_enum NOT NULL DEFAULT 'active',
  created_by       uuid,
  approved_by      uuid,
  created_at       timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_psv_pay   CHECK (max_basic_pay >= min_basic_pay),
  CONSTRAINT ck_psv_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ex_psv_no_overlap EXCLUDE USING gist (
    grade_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  ) WHERE (status = 'active')
);
COMMENT ON TABLE pay_scale_version IS
  'Versioned pay-scale revisions: a new notification creates a new row. The per-year increment amount is NOT stored here (it was redundant); pay_scale_stage is the single source of truth for stage amounts.';

CREATE TABLE pay_scale_stage (
  id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  pay_scale_version_id uuid NOT NULL REFERENCES pay_scale_version(id),
  stage_number         int NOT NULL CHECK (stage_number >= 0),
  basic_pay            numeric(14,2) NOT NULL CHECK (basic_pay >= 0),
  UNIQUE (pay_scale_version_id, stage_number)
);
COMMENT ON TABLE pay_scale_stage IS 'Annual-increment stage within a pay-scale version, e.g. BPS-17 Stage 4 basic pay.';

-- =====================================================================
-- MODULE 3: SANCTIONED POST
-- =====================================================================
CREATE TABLE post (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  post_code  varchar NOT NULL UNIQUE,
  created_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid
);
COMMENT ON TABLE post IS 'Immutable identity of a sanctioned post. Mutable attributes live in post_version.';

CREATE TABLE post_version (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  post_id          uuid NOT NULL REFERENCES post(id),
  designation_id   uuid NOT NULL REFERENCES designation(id),
  grade_id         uuid NOT NULL REFERENCES pay_scale_grade(id),
  org_unit_id      uuid NOT NULL REFERENCES organization_unit(id),
  reporting_post_id uuid REFERENCES post(id),
  employment_type  employment_type_enum NOT NULL DEFAULT 'regular',
  location_id      uuid REFERENCES location(id),
  sanctioned_count int NOT NULL DEFAULT 1 CHECK (sanctioned_count >= 1),
  lifecycle_status post_lifecycle_enum NOT NULL DEFAULT 'sanctioned',
  notification_ref varchar,
  effective_from   date NOT NULL,
  effective_to     date,
  created_at       timestamptz NOT NULL DEFAULT now(),
  created_by       uuid,
  approved_by      uuid,
  CONSTRAINT ck_pv_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ck_pv_self  CHECK (reporting_post_id IS NULL OR reporting_post_id <> post_id),
  CONSTRAINT ex_pv_no_overlap EXCLUDE USING gist (
    post_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  )
);
CREATE INDEX idx_pv_org_unit  ON post_version (org_unit_id);
CREATE INDEX idx_pv_reporting ON post_version (reporting_post_id);
COMMENT ON TABLE post_version IS
  'One version of a post''s attributes. sanctioned_count = number of seats this post identity carries (1 = a single seat; >1 = a pool such as "10 Drivers"). Whether those seats are vacant/filled is DERIVED in v_post_occupancy from position_assignment - it is no longer stored, so it cannot drift.';

ALTER TABLE organization_unit_version
  ADD CONSTRAINT fk_ouv_head_post FOREIGN KEY (head_post_id) REFERENCES post(id);

-- Current org-unit attributes (convenience view; depth computed from parent chain)
CREATE VIEW v_organization_unit_current AS
WITH RECURSIVE cur AS (
  SELECT v.*, u.code
    FROM organization_unit_version v
    JOIN organization_unit u ON u.id = v.org_unit_id
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
-- =====================================================================
-- MODULE 4: EMPLOYEE MASTER
-- =====================================================================
CREATE TABLE employee (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_number   varchar NOT NULL UNIQUE,
  user_id           uuid UNIQUE,             -- platform users.id, no cross-schema FK
  employment_type   employment_type_enum NOT NULL,
  employment_method employment_method_enum,  -- only when employment_type = 'regular'
  project_id        uuid,                    -- Project module not merged yet, no FK

  first_name   varchar NOT NULL,
  middle_name  varchar,
  last_name    varchar,
  full_name    varchar GENERATED ALWAYS AS (
                 first_name || coalesce(' ' || middle_name, '') || coalesce(' ' || last_name, '')
               ) STORED,

  cnic            varchar NOT NULL UNIQUE,
  date_of_birth   date,
  gender          gender_enum,
  nationality     varchar DEFAULT 'Pakistani',
  marital_status  marital_status_enum,
  blood_group     varchar,

  profile_status    record_status_enum NOT NULL DEFAULT 'active',
  employment_status employment_status_enum NOT NULL DEFAULT 'active',
  photo_reference   varchar,

  created_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_at timestamptz,
  updated_by uuid,

  CONSTRAINT ck_emp_method CHECK (
    (employment_type = 'regular' AND employment_method IS NOT NULL) OR
    (employment_type <> 'regular' AND employment_method IS NULL)
  ),
  CONSTRAINT ck_emp_cnic CHECK (cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'),
  CONSTRAINT ck_emp_dob  CHECK (date_of_birth IS NULL OR date_of_birth < current_date),
  CONSTRAINT ck_emp_blood CHECK (blood_group IS NULL OR blood_group IN ('A+','A-','B+','B-','AB+','AB-','O+','O-'))
);
CREATE INDEX idx_employee_type   ON employee (employment_type);
CREATE INDEX idx_employee_status ON employee (employment_status);
COMMENT ON TABLE employee IS 'Core identity only. full_name is a generated column. CNIC is stored in plain text (needed for uniqueness/search): protect with column privileges (see security section) and disk/backup encryption.';
COMMENT ON COLUMN employee.cnic IS 'Accepts 13 digits with or without dashes (35202-1234567-1). Normalise in the application so the UNIQUE index is meaningful.';

-- Current basic pay / BPS stage of each employee (was missing: increments could not be computed)
CREATE TABLE employee_pay_record (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id        uuid NOT NULL REFERENCES employee(id),
  pay_scale_stage_id uuid NOT NULL REFERENCES pay_scale_stage(id),
  basic_pay          numeric(14,2) NOT NULL CHECK (basic_pay >= 0),
  last_increment_date date,
  next_increment_date date,
  reason             varchar,                -- appointment / annual increment / promotion / pay revision ...
  order_number       varchar,
  effective_from     date NOT NULL,
  effective_to       date,
  created_at         timestamptz NOT NULL DEFAULT now(),
  created_by         uuid,
  CONSTRAINT ck_epr_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ex_epr_no_overlap EXCLUDE USING gist (
    employee_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  )
);
COMMENT ON TABLE employee_pay_record IS
  'Effective-dated pay position of an employee (stage + actual basic pay). Annual increments close the current row and open the next stage. Payroll reads Basic Pay from here.';

CREATE TABLE employee_contact (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id  uuid NOT NULL REFERENCES employee(id),
  contact_type contact_type_enum NOT NULL,
  value        varchar NOT NULL,
  is_primary   boolean NOT NULL DEFAULT false,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_employee_contact_primary ON employee_contact (employee_id, contact_type) WHERE is_primary;

CREATE TABLE employee_address (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id  uuid NOT NULL REFERENCES employee(id),
  address_type address_type_enum NOT NULL,
  address_line varchar,
  city         varchar,
  district     varchar,
  province     varchar,
  postal_code  varchar,
  created_at   timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX uq_employee_address_type ON employee_address (employee_id, address_type) WHERE address_type IN ('permanent','current');

CREATE TABLE employee_emergency_contact (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id uuid NOT NULL REFERENCES employee(id),
  name        varchar NOT NULL,
  relation    varchar,
  phone       varchar,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE employee_family_member (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id   uuid NOT NULL REFERENCES employee(id),
  relation      varchar,
  name          varchar,
  date_of_birth date,
  cnic          varchar CHECK (cnic IS NULL OR cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'),
  occupation    varchar,
  created_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE employee_bank_account (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id    uuid NOT NULL REFERENCES employee(id),
  bank_name      varchar NOT NULL,
  branch_name    varchar,
  account_number varchar,
  iban           varchar,
  is_primary     boolean NOT NULL DEFAULT true,
  status         record_status_enum NOT NULL DEFAULT 'active',
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     uuid,
  updated_at     timestamptz,
  updated_by     uuid,
  UNIQUE (id, employee_id),                  -- lets payroll_payment prove ownership
  CONSTRAINT ck_eba_iban CHECK (iban IS NULL OR iban ~ '^PK[0-9]{2}[A-Z]{4}[0-9]{16}$'),
  CONSTRAINT ck_eba_ident CHECK (account_number IS NOT NULL OR iban IS NOT NULL)
);
CREATE UNIQUE INDEX uq_eba_one_primary ON employee_bank_account (employee_id) WHERE is_primary AND status = 'active';
COMMENT ON TABLE employee_bank_account IS 'Restricted-access table; not joined into general reporting views. Payment rows snapshot these values at payment time.';

-- =====================================================================
-- MODULE 5: DOCUMENTS & EDUCATION
-- =====================================================================
CREATE TABLE document_type (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name            varchar NOT NULL UNIQUE,
  requires_expiry boolean NOT NULL DEFAULT false,
  is_active       boolean NOT NULL DEFAULT true
);

CREATE TABLE employee_document (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id         uuid NOT NULL REFERENCES employee(id),
  document_type_id    uuid NOT NULL REFERENCES document_type(id),
  document_number     varchar,
  issue_date          date,
  expiry_date         date,
  file_reference      varchar NOT NULL,
  verification_status verification_status_enum NOT NULL DEFAULT 'unverified',
  verified_by         uuid,
  verification_date   date,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid,
  CONSTRAINT ck_ed_dates CHECK (expiry_date IS NULL OR issue_date IS NULL OR expiry_date >= issue_date),
  CONSTRAINT ck_ed_verified CHECK (verification_status <> 'verified' OR (verified_by IS NOT NULL AND verification_date IS NOT NULL))
);
CREATE INDEX idx_ed_employee ON employee_document (employee_id, document_type_id);
CREATE INDEX idx_ed_expiry   ON employee_document (expiry_date) WHERE expiry_date IS NOT NULL;

CREATE TABLE employee_education (
  id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id              uuid NOT NULL REFERENCES employee(id),
  qualification_level      qualification_level_enum NOT NULL,
  degree_title             varchar NOT NULL,
  institution_name         varchar NOT NULL,
  board_or_university      varchar,
  passing_year             int CHECK (passing_year IS NULL OR passing_year BETWEEN 1950 AND 2100),
  grade_or_cgpa            varchar,
  is_highest_qualification boolean NOT NULL DEFAULT false,
  file_reference           varchar,
  created_at               timestamptz NOT NULL DEFAULT now(),
  created_by               uuid
);
CREATE INDEX idx_edu_employee_level ON employee_education (employee_id, qualification_level);
CREATE UNIQUE INDEX uq_edu_one_highest ON employee_education (employee_id) WHERE is_highest_qualification;

-- =====================================================================
-- MODULE 6: RECRUITMENT (lightweight)
-- =====================================================================
CREATE TABLE recruitment_method (
  id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name      varchar NOT NULL UNIQUE,
  is_active boolean NOT NULL DEFAULT true
);
COMMENT ON TABLE recruitment_method IS 'Project Regularization / Board of Authority / Public Service (CSS, PMS ...). Referenced from employee_service_history.';
-- =====================================================================
-- MODULE 7: EMPLOYMENT HISTORY
-- =====================================================================
CREATE TABLE service_event_type (
  id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name      varchar NOT NULL UNIQUE,
  category  varchar,
  is_active boolean NOT NULL DEFAULT true
);

CREATE TABLE employee_service_history (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id            uuid NOT NULL REFERENCES employee(id),
  event_type_id          uuid NOT NULL REFERENCES service_event_type(id),
  effective_date         date NOT NULL,

  old_post_id            uuid REFERENCES post(id),
  new_post_id            uuid REFERENCES post(id),
  old_grade_id           uuid REFERENCES pay_scale_grade(id),
  new_grade_id           uuid REFERENCES pay_scale_grade(id),
  old_org_unit_id        uuid REFERENCES organization_unit(id),
  new_org_unit_id        uuid REFERENCES organization_unit(id),

  recruitment_method_id  uuid REFERENCES recruitment_method(id),
  external_reference_org varchar,            -- counterpart dept for Deputation In/Out
  order_number           varchar,
  supporting_document_id uuid REFERENCES employee_document(id),

  reason     text,
  remarks    text,
  created_by uuid,
  approved_by uuid,
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_esh_employee_date ON employee_service_history (employee_id, effective_date);
CREATE INDEX idx_esh_event         ON employee_service_history (event_type_id);
CREATE TRIGGER trg_esh_append_only
  BEFORE UPDATE OR DELETE ON employee_service_history
  FOR EACH ROW EXECUTE FUNCTION fn_block_mutation();
COMMENT ON TABLE employee_service_history IS
  'Append-only service ledger: ONE row per service event (appointment, transfer, promotion, deputation ...). It records WHAT happened and why. The resulting occupancy of a post is recorded in position_assignment (the two tables complement each other). UPDATE/DELETE are blocked by trigger; corrections are offsetting rows.';

CREATE TABLE position_assignment (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  post_id           uuid NOT NULL REFERENCES post(id),
  employee_id       uuid NOT NULL REFERENCES employee(id),
  assignment_type   assignment_type_enum NOT NULL DEFAULT 'regular',
  service_history_id uuid REFERENCES employee_service_history(id),
  order_number      varchar,
  order_document_id uuid REFERENCES employee_document(id),
  effective_from    date NOT NULL,
  effective_to      date,
  status            record_status_enum NOT NULL DEFAULT 'active',
  remarks           text,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        uuid,
  CONSTRAINT ck_pa_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  -- an employee holds at most ONE regular post at a time (acting / additional charge may coexist)
  CONSTRAINT ex_pa_one_regular EXCLUDE USING gist (
    employee_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  ) WHERE (assignment_type = 'regular' AND status = 'active')
);
CREATE INDEX idx_pa_post_from ON position_assignment (post_id, effective_from);
CREATE INDEX idx_pa_employee  ON position_assignment (employee_id);
COMMENT ON TABLE position_assignment IS
  'Employee occupancy of a sanctioned post. Acting / additional-charge assignments can coexist with a regular one. A trigger stops regular assignments exceeding the post''s sanctioned_count and refuses frozen / abolished posts.';

CREATE OR REPLACE FUNCTION fn_check_post_capacity() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
  v_seats int; v_life post_lifecycle_enum; v_taken int;
BEGIN
  IF NEW.assignment_type <> 'regular' OR NEW.status <> 'active' THEN
    RETURN NEW;
  END IF;

  SELECT sanctioned_count, lifecycle_status INTO v_seats, v_life
    FROM post_version
   WHERE post_id = NEW.post_id
     AND NEW.effective_from BETWEEN effective_from AND coalesce(effective_to, 'infinity'::date);
  IF NOT FOUND THEN
    RAISE EXCEPTION 'post % has no post_version in effect on %', NEW.post_id, NEW.effective_from;
  END IF;
  IF v_life <> 'sanctioned' THEN
    RAISE EXCEPTION 'post % is % and cannot receive a regular assignment', NEW.post_id, v_life;
  END IF;

  SELECT count(*) INTO v_taken
    FROM position_assignment pa
   WHERE pa.post_id = NEW.post_id
     AND pa.assignment_type = 'regular' AND pa.status = 'active'
     AND pa.id IS DISTINCT FROM NEW.id
     AND daterange(pa.effective_from, pa.effective_to, '[]') && daterange(NEW.effective_from, NEW.effective_to, '[]');
  IF v_taken >= v_seats THEN
    RAISE EXCEPTION 'post % is full: % of % seats already held in the requested period', NEW.post_id, v_taken, v_seats;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_pa_capacity
  BEFORE INSERT OR UPDATE ON position_assignment
  FOR EACH ROW EXECUTE FUNCTION fn_check_post_capacity();

-- Derived occupancy replaces the old stored post_version.status
CREATE VIEW v_post_occupancy AS
SELECT pv.post_id, p.post_code, pv.designation_id, pv.grade_id, pv.org_unit_id,
       pv.sanctioned_count,
       coalesce(h.cnt, 0) AS filled_count,
       CASE
         WHEN pv.lifecycle_status = 'abolished' THEN 'abolished'::position_status_enum
         WHEN pv.lifecycle_status = 'frozen'    THEN 'frozen'::position_status_enum
         WHEN coalesce(h.cnt,0) = 0                  THEN 'vacant'::position_status_enum
         WHEN coalesce(h.cnt,0) < pv.sanctioned_count THEN 'partially_filled'::position_status_enum
         ELSE 'filled'::position_status_enum
       END AS status
  FROM post_version pv
  JOIN post p ON p.id = pv.post_id
  LEFT JOIN LATERAL (
    SELECT count(*) AS cnt
      FROM position_assignment pa
     WHERE pa.post_id = pv.post_id AND pa.assignment_type = 'regular' AND pa.status = 'active'
       AND current_date BETWEEN pa.effective_from AND coalesce(pa.effective_to, 'infinity'::date)
  ) h ON true
 WHERE current_date BETWEEN pv.effective_from AND coalesce(pv.effective_to, 'infinity'::date);

CREATE TABLE hr_action_request (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  action_type      hr_action_type_enum NOT NULL,
  employee_id      uuid NOT NULL REFERENCES employee(id),
  old_post_id      uuid REFERENCES post(id),
  new_post_id      uuid REFERENCES post(id),
  old_grade_id     uuid REFERENCES pay_scale_grade(id),
  new_grade_id     uuid REFERENCES pay_scale_grade(id),
  old_org_unit_id  uuid REFERENCES organization_unit(id),
  new_org_unit_id  uuid REFERENCES organization_unit(id),
  effective_date   date NOT NULL,
  order_number     varchar,
  reason           text,
  supporting_document_id uuid REFERENCES employee_document(id),
  status           hr_action_status_enum NOT NULL DEFAULT 'draft',
  approval_request_id uuid,                  -- pointer into the platform's generic approval workflow
  approved_by      uuid,
  approved_at      timestamptz,
  resulting_service_history_id uuid REFERENCES employee_service_history(id),
  created_by       uuid,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_at       timestamptz,
  updated_by       uuid,
  CONSTRAINT ck_har_applied CHECK (status <> 'applied' OR resulting_service_history_id IS NOT NULL)
);
CREATE INDEX idx_har_employee ON hr_action_request (employee_id, effective_date);
CREATE INDEX idx_har_status   ON hr_action_request (status);
COMMENT ON TABLE hr_action_request IS
  'Draft/pending HR change. Routed through the platform approval workflow (approval_request_id is a plain uuid pointer). When applied, an employee_service_history row is written and linked in resulting_service_history_id.';

CREATE TABLE employee_separation (
  id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id             uuid NOT NULL REFERENCES employee(id),
  separation_type         separation_type_enum NOT NULL,
  separation_date         date NOT NULL,
  service_history_id      uuid REFERENCES employee_service_history(id),
  settlement_payroll_transaction_id uuid,    -- FK to payroll_transaction added in payroll module
  order_number            varchar,
  reason                  text,
  notice_period_days      int CHECK (notice_period_days IS NULL OR notice_period_days >= 0),
  final_settlement_amount numeric(14,2),
  outstanding_loan_amount numeric(14,2) CHECK (outstanding_loan_amount IS NULL OR outstanding_loan_amount >= 0),
  leave_encashment_amount numeric(14,2) CHECK (leave_encashment_amount IS NULL OR leave_encashment_amount >= 0),
  pension_reference       varchar,
  created_at              timestamptz NOT NULL DEFAULT now(),
  created_by              uuid
);
CREATE UNIQUE INDEX uq_separation_employee ON employee_separation (employee_id);

-- =====================================================================
-- MODULE 8: PERFORMANCE MANAGEMENT
-- =====================================================================
CREATE TABLE performance_period (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name       varchar NOT NULL,
  start_date date NOT NULL,
  end_date   date NOT NULL,
  status     record_status_enum NOT NULL DEFAULT 'active',
  CONSTRAINT ck_pp_dates CHECK (end_date >= start_date),
  CONSTRAINT ex_pp_no_overlap EXCLUDE USING gist (daterange(start_date, end_date, '[]') WITH &&) WHERE (status = 'active')
);

CREATE TABLE performance_review (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id           uuid NOT NULL REFERENCES employee(id),
  performance_period_id uuid NOT NULL REFERENCES performance_period(id),
  evaluator_id          uuid NOT NULL REFERENCES employee(id),
  status                review_status_enum NOT NULL DEFAULT 'draft',
  final_score           numeric(6,2) CHECK (final_score IS NULL OR final_score BETWEEN 0 AND 100),
  performance_grade     varchar,
  promotion_recommended boolean NOT NULL DEFAULT false,
  training_recommended  boolean NOT NULL DEFAULT false,
  remarks               text,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid,
  updated_at            timestamptz,
  updated_by            uuid,
  UNIQUE (employee_id, performance_period_id),
  CONSTRAINT ck_pr_not_self CHECK (employee_id <> evaluator_id)
);
CREATE INDEX idx_pr_evaluator ON performance_review (evaluator_id);

CREATE TABLE performance_goal (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  performance_review_id uuid NOT NULL REFERENCES performance_review(id) ON DELETE CASCADE,
  description           text NOT NULL,
  weight                numeric(5,2) NOT NULL DEFAULT 0 CHECK (weight BETWEEN 0 AND 100),
  target                text,
  achievement           text,
  score                 numeric(6,2) CHECK (score IS NULL OR score BETWEEN 0 AND 100)
);

CREATE TABLE performance_kpi (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  performance_review_id uuid NOT NULL REFERENCES performance_review(id) ON DELETE CASCADE,
  kpi_name              varchar NOT NULL,
  target_value          varchar,
  achieved_value        varchar,
  score                 numeric(6,2) CHECK (score IS NULL OR score BETWEEN 0 AND 100)
);

CREATE TABLE performance_competency (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  performance_review_id uuid NOT NULL REFERENCES performance_review(id) ON DELETE CASCADE,
  competency_name       varchar NOT NULL,
  rating                numeric(4,2) CHECK (rating IS NULL OR rating BETWEEN 0 AND 10),
  remarks               text
);

-- Goal weights must total 100 before a review can be submitted / finalized
CREATE OR REPLACE FUNCTION fn_check_goal_weights() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_total numeric; v_cnt int;
BEGIN
  IF NEW.status IN ('submitted','acknowledged','finalized') AND
     (TG_OP = 'INSERT' OR OLD.status = 'draft') THEN
    SELECT coalesce(sum(weight),0), count(*) INTO v_total, v_cnt
      FROM performance_goal WHERE performance_review_id = NEW.id;
    IF v_cnt > 0 AND v_total <> 100 THEN
      RAISE EXCEPTION 'goal weights for review % total %, must be 100', NEW.id, v_total;
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_pr_goal_weights
  BEFORE INSERT OR UPDATE OF status ON performance_review
  FOR EACH ROW EXECUTE FUNCTION fn_check_goal_weights();
-- =====================================================================
-- MODULE 9: ATTENDANCE, SHIFTS, HOLIDAYS
-- =====================================================================
CREATE TABLE work_shift (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name             varchar NOT NULL UNIQUE,
  start_time       time NOT NULL,
  end_time         time NOT NULL,
  grace_minutes    int NOT NULL DEFAULT 0 CHECK (grace_minutes >= 0),
  working_weekdays smallint[] NOT NULL DEFAULT '{1,2,3,4,5}',   -- ISO: 1=Mon .. 7=Sun
  is_active        boolean NOT NULL DEFAULT true,
  CONSTRAINT ck_ws_days CHECK (working_weekdays <@ ARRAY[1,2,3,4,5,6,7]::smallint[] AND cardinality(working_weekdays) > 0)
);

CREATE TABLE employee_shift (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id    uuid NOT NULL REFERENCES employee(id),
  work_shift_id  uuid NOT NULL REFERENCES work_shift(id),
  effective_from date NOT NULL,
  effective_to   date,
  CONSTRAINT ck_es_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ex_es_no_overlap EXCLUDE USING gist (
    employee_id WITH =, daterange(effective_from, effective_to, '[]') WITH &&
  )
);

CREATE TABLE holiday_calendar (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  holiday_date date NOT NULL,
  name         varchar NOT NULL,
  holiday_type holiday_type_enum NOT NULL DEFAULT 'public',
  location_id  uuid REFERENCES location(id),     -- NULL = applies everywhere
  notification_ref varchar,
  UNIQUE NULLS NOT DISTINCT (holiday_date, name, location_id)
);
CREATE INDEX idx_holiday_date ON holiday_calendar (holiday_date);

CREATE TABLE attendance_record (
  id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id             uuid NOT NULL REFERENCES employee(id),
  attendance_date         date NOT NULL,
  work_shift_id           uuid REFERENCES work_shift(id),
  check_in                time,
  check_out               time,
  working_hours           numeric(5,2) CHECK (working_hours IS NULL OR working_hours BETWEEN 0 AND 24),
  overtime_hours          numeric(5,2) CHECK (overtime_hours IS NULL OR overtime_hours BETWEEN 0 AND 24),
  late_minutes            int NOT NULL DEFAULT 0 CHECK (late_minutes >= 0),
  early_departure_minutes int NOT NULL DEFAULT 0 CHECK (early_departure_minutes >= 0),
  status                  attendance_status_enum,
  remarks                 text,
  created_at              timestamptz NOT NULL DEFAULT now(),
  updated_at              timestamptz,
  UNIQUE (employee_id, attendance_date)
);
CREATE INDEX idx_att_date ON attendance_record (attendance_date);

-- =====================================================================
-- MODULE 10: LEAVE MANAGEMENT
-- =====================================================================
CREATE TABLE leave_type (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name                  varchar NOT NULL UNIQUE,
  is_paid               boolean NOT NULL DEFAULT true,
  max_days_per_year     numeric(6,2) CHECK (max_days_per_year IS NULL OR max_days_per_year >= 0),
  accrual_rule          text,
  carry_forward_allowed boolean NOT NULL DEFAULT false,
  affects_payroll       boolean NOT NULL DEFAULT false      -- true for LWOP-style types: days are deducted from days_payable
);

-- Yearly entitlement; running balances are computed from leave_ledger (no denormalised balance column)
CREATE TABLE leave_entitlement (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id   uuid NOT NULL REFERENCES employee(id),
  leave_type_id uuid NOT NULL REFERENCES leave_type(id),
  year          int NOT NULL CHECK (year BETWEEN 2000 AND 2100),
  entitled_days numeric(6,2) NOT NULL DEFAULT 0 CHECK (entitled_days >= 0),
  created_at    timestamptz NOT NULL DEFAULT now(),
  created_by    uuid,
  UNIQUE (employee_id, leave_type_id, year)
);

CREATE TABLE leave_application (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id   uuid NOT NULL REFERENCES employee(id),
  leave_type_id uuid NOT NULL REFERENCES leave_type(id),
  start_date    date NOT NULL,
  end_date      date NOT NULL,
  days          numeric(6,2) NOT NULL CHECK (days > 0),
  status        leave_status_enum NOT NULL DEFAULT 'pending',
  applied_date  date NOT NULL DEFAULT current_date,
  approved_by   uuid,                        -- platform users.id
  approval_date date,
  reason        text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz,
  updated_by    uuid,
  CONSTRAINT ck_la_dates    CHECK (end_date >= start_date),
  CONSTRAINT ck_la_approval CHECK (status <> 'approved' OR (approved_by IS NOT NULL AND approval_date IS NOT NULL)),
  CONSTRAINT ex_la_no_overlap EXCLUDE USING gist (
    employee_id WITH =, daterange(start_date, end_date, '[]') WITH &&
  ) WHERE (status IN ('pending','approved'))
);
CREATE INDEX idx_la_employee_start ON leave_application (employee_id, start_date);
CREATE INDEX idx_la_status         ON leave_application (status);

CREATE TABLE leave_ledger (
  id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id          uuid NOT NULL REFERENCES employee(id),
  leave_type_id        uuid NOT NULL REFERENCES leave_type(id),
  year                 int NOT NULL,
  txn_type             leave_txn_type_enum NOT NULL,
  days                 numeric(6,2) NOT NULL CHECK (days <> 0),   -- signed: + credit, - debit
  txn_date             date NOT NULL DEFAULT current_date,
  leave_application_id uuid REFERENCES leave_application(id),
  remarks              text,
  created_at           timestamptz NOT NULL DEFAULT now(),
  created_by           uuid,
  CONSTRAINT ck_ll_sign CHECK (
    (txn_type IN ('opening','accrual','carry_forward','usage_reversal') AND days > 0) OR
    (txn_type IN ('usage','lapse','encashment') AND days < 0) OR
    txn_type = 'adjustment'
  ),
  CONSTRAINT ck_ll_app CHECK (txn_type NOT IN ('usage','usage_reversal') OR leave_application_id IS NOT NULL)
);
CREATE INDEX idx_ll_emp_type_year ON leave_ledger (employee_id, leave_type_id, year);
CREATE TRIGGER trg_leave_ledger_append_only
  BEFORE UPDATE OR DELETE ON leave_ledger
  FOR EACH ROW EXECUTE FUNCTION fn_block_mutation();
COMMENT ON TABLE leave_ledger IS 'Append-only leave transactions (accrual, usage, carry-forward, lapse, encashment, adjustment). Balances are derived, so they can always be audited.';

CREATE VIEW v_leave_balance AS
SELECT e.employee_id, e.leave_type_id, e.year,
       e.entitled_days,
       coalesce(sum(l.days) FILTER (WHERE l.txn_type IN ('opening','accrual','carry_forward')), 0)  AS accrued_days,
       -coalesce(sum(l.days) FILTER (WHERE l.txn_type IN ('usage','usage_reversal')), 0)            AS used_days,
       e.entitled_days + coalesce(sum(l.days), 0)                                                    AS balance_days
  FROM leave_entitlement e
  LEFT JOIN leave_ledger l
         ON l.employee_id = e.employee_id AND l.leave_type_id = e.leave_type_id AND l.year = e.year
 GROUP BY e.employee_id, e.leave_type_id, e.year, e.entitled_days;
COMMENT ON VIEW v_leave_balance IS 'balance_days = entitled_days + signed ledger total. If you credit entitlement via ledger "accrual" rows instead, set entitled_days = 0.';

-- Payable days for an employee in a payroll period = days in period (clipped to employment
-- window) minus approved leave of types that affect payroll (e.g. LWOP).
CREATE OR REPLACE FUNCTION fn_days_payable(p_employee uuid, p_from date, p_to date, p_join date DEFAULT NULL, p_leave date DEFAULT NULL)
RETURNS numeric LANGUAGE sql STABLE AS $$
  WITH win AS (
    SELECT greatest(p_from, coalesce(p_join, p_from)) AS d1,
           least(p_to,   coalesce(p_leave, p_to))     AS d2
  ), unpaid AS (
    SELECT coalesce(sum(
             greatest(0, least(la.end_date, w.d2) - greatest(la.start_date, w.d1) + 1)
           ), 0) AS d
      FROM win w
      JOIN leave_application la ON la.employee_id = p_employee AND la.status = 'approved'
      JOIN leave_type lt ON lt.id = la.leave_type_id AND lt.affects_payroll
     WHERE la.start_date <= w.d2 AND la.end_date >= w.d1
  )
  SELECT greatest(0, (w.d2 - w.d1 + 1) - u.d)::numeric FROM win w, unpaid u;
$$;
-- =====================================================================
-- MODULE 11: SALARY STRUCTURE
-- =====================================================================
CREATE TABLE salary_component (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  component_code varchar NOT NULL UNIQUE,
  component_name varchar NOT NULL,
  component_type component_type_enum NOT NULL,
  is_taxable     boolean NOT NULL DEFAULT true,
  is_active      boolean NOT NULL DEFAULT true
);
COMMENT ON TABLE salary_component IS
  'Pay components from the Officers'' pay slip. EARNING: Basic Pay, GDA Authority Allowance, Medical Allowance, Housing Subsidy, Utility Allowance, Adhoc Relief Allowance (one row per notification year). DEDUCTION: BF, GP Fund, MCA, GPF Advance, RB&D, Income Tax.';

CREATE TABLE salary_component_rule (
  id                        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  salary_component_id       uuid NOT NULL REFERENCES salary_component(id),
  rule_version              varchar NOT NULL,
  calculation_method        calculation_method_enum NOT NULL,
  fixed_amount              numeric(14,2) CHECK (fixed_amount IS NULL OR fixed_amount >= 0),
  percentage                numeric(6,3)  CHECK (percentage IS NULL OR percentage BETWEEN 0 AND 1000),
  calculation_base          varchar,       -- basic_pay, gross_pay ...
  formula_expression        text,
  min_bps                   int CHECK (min_bps IS NULL OR min_bps BETWEEN 1 AND 22),
  max_bps                   int CHECK (max_bps IS NULL OR max_bps BETWEEN 1 AND 22),
  applicable_designation_id uuid REFERENCES designation(id),
  applicable_org_unit_id    uuid REFERENCES organization_unit(id),
  applicable_employment_type employment_type_enum,
  min_amount                numeric(14,2),
  max_amount                numeric(14,2),
  priority                  int NOT NULL DEFAULT 100,    -- lower number wins when several rules match
  notification_ref          varchar,
  effective_from            date NOT NULL,
  effective_to              date,
  status                    record_status_enum NOT NULL DEFAULT 'active',
  created_by                uuid,
  approved_by               uuid,
  created_at                timestamptz NOT NULL DEFAULT now(),
  UNIQUE (salary_component_id, rule_version),
  CONSTRAINT ck_scr_dates  CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ck_scr_bps    CHECK (min_bps IS NULL OR max_bps IS NULL OR min_bps <= max_bps),
  CONSTRAINT ck_scr_amount CHECK (min_amount IS NULL OR max_amount IS NULL OR min_amount <= max_amount),
  CONSTRAINT ck_scr_method CHECK (
    (calculation_method = 'fixed'      AND fixed_amount IS NOT NULL) OR
    (calculation_method = 'percentage' AND percentage IS NOT NULL AND calculation_base IS NOT NULL) OR
    (calculation_method = 'formula'    AND formula_expression IS NOT NULL) OR
    (calculation_method = 'tiered')
  )
);
CREATE INDEX idx_scr_component_from ON salary_component_rule (salary_component_id, effective_from);
COMMENT ON TABLE salary_component_rule IS
  'Versioned, scoped rules. Several rules of one component may overlap in time when their scope differs (designation / BPS range / org unit / employment type); priority breaks ties, so no time-exclusion is enforced here. Each Adhoc Relief year is its own component.';

CREATE TABLE employee_salary_component (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id           uuid NOT NULL REFERENCES employee(id),
  salary_component_id   uuid NOT NULL REFERENCES salary_component(id),
  post_id               uuid REFERENCES post(id),     -- optional: tie the override to a post
  override_fixed_amount numeric(14,2) CHECK (override_fixed_amount IS NULL OR override_fixed_amount >= 0),
  override_percentage   numeric(6,3)  CHECK (override_percentage IS NULL OR override_percentage BETWEEN 0 AND 1000),
  effective_from        date NOT NULL,
  effective_to          date,
  remarks               text,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            uuid,
  CONSTRAINT ck_esc_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ck_esc_one   CHECK (num_nonnulls(override_fixed_amount, override_percentage) = 1),
  CONSTRAINT ex_esc_no_overlap EXCLUDE USING gist (
    employee_id WITH =, salary_component_id WITH =,
    daterange(effective_from, effective_to, '[]') WITH &&
  )
);
COMMENT ON TABLE employee_salary_component IS 'Employee-specific overrides of the standard rule. Exactly one of fixed amount / percentage.';

-- =====================================================================
-- MODULE 12: INCOME TAX
-- =====================================================================
CREATE TABLE tax_year (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  year_label varchar NOT NULL UNIQUE,
  start_date date NOT NULL,
  end_date   date NOT NULL,
  status     record_status_enum NOT NULL DEFAULT 'active',
  CONSTRAINT ck_ty_dates CHECK (end_date > start_date),
  CONSTRAINT ex_ty_no_overlap EXCLUDE USING gist (daterange(start_date, end_date, '[]') WITH &&)
);

CREATE TABLE tax_slab (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  tax_year_id     uuid NOT NULL REFERENCES tax_year(id),
  slab_order      int NOT NULL,
  min_income      numeric(14,2) NOT NULL CHECK (min_income >= 0),
  max_income      numeric(14,2),
  fixed_amount    numeric(14,2) NOT NULL DEFAULT 0 CHECK (fixed_amount >= 0),
  rate_percentage numeric(6,3)  NOT NULL DEFAULT 0 CHECK (rate_percentage BETWEEN 0 AND 100),
  UNIQUE (tax_year_id, slab_order),
  CONSTRAINT ck_slab_range CHECK (max_income IS NULL OR max_income > min_income),
  CONSTRAINT ex_slab_no_overlap EXCLUDE USING gist (tax_year_id WITH =, numrange(min_income, max_income, '[)') WITH &&)
);

CREATE TABLE employee_tax_exemption (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id    uuid NOT NULL REFERENCES employee(id),
  tax_year_id    uuid NOT NULL REFERENCES tax_year(id),
  exemption_type varchar NOT NULL,
  amount         numeric(14,2) NOT NULL CHECK (amount >= 0),
  UNIQUE (employee_id, tax_year_id, exemption_type)
);

-- =====================================================================
-- MODULE 13: LOANS / ADVANCES
-- =====================================================================
CREATE TABLE loan_type (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name                  varchar NOT NULL UNIQUE,
  salary_component_id   uuid REFERENCES salary_component(id),   -- deduction component this loan appears as on the pay slip
  is_gpf_advance        boolean NOT NULL DEFAULT false,
  default_interest_rate numeric(6,3) NOT NULL DEFAULT 0 CHECK (default_interest_rate >= 0),
  is_active             boolean NOT NULL DEFAULT true
);
COMMENT ON TABLE loan_type IS 'e.g. Motor Car Advance (MCA), GP Fund Advance. salary_component_id says which pay-slip deduction line carries the installment, so the loan is never double counted.';

CREATE TABLE employee_loan (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id         uuid NOT NULL REFERENCES employee(id),
  loan_type_id        uuid NOT NULL REFERENCES loan_type(id),
  principal_amount    numeric(14,2) NOT NULL CHECK (principal_amount > 0),
  interest_amount     numeric(14,2) NOT NULL DEFAULT 0 CHECK (interest_amount >= 0),
  installments_count  int NOT NULL CHECK (installments_count > 0),
  monthly_installment numeric(14,2) NOT NULL CHECK (monthly_installment > 0),
  start_date          date NOT NULL,
  end_date            date,
  remaining_balance   numeric(14,2) NOT NULL CHECK (remaining_balance >= 0),
  deduction_priority  int NOT NULL DEFAULT 100,
  status              loan_status_enum NOT NULL DEFAULT 'active',
  approved_by         uuid,
  created_at          timestamptz NOT NULL DEFAULT now(),
  created_by          uuid,
  updated_at          timestamptz,
  updated_by          uuid,
  CONSTRAINT ck_el_dates CHECK (end_date IS NULL OR end_date >= start_date)
);
CREATE INDEX idx_el_employee_status ON employee_loan (employee_id, status);

CREATE TABLE loan_installment_schedule (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_loan_id   uuid NOT NULL REFERENCES employee_loan(id),
  installment_number int NOT NULL CHECK (installment_number > 0),
  due_date           date NOT NULL,
  amount             numeric(14,2) NOT NULL CHECK (amount > 0),
  paid_amount        numeric(14,2) NOT NULL DEFAULT 0 CHECK (paid_amount >= 0),
  status             installment_status_enum NOT NULL DEFAULT 'pending',
  UNIQUE (employee_loan_id, installment_number),
  CONSTRAINT uq_lis_id_loan UNIQUE (id, employee_loan_id),      -- target of payroll_loan_deduction composite FK
  CONSTRAINT ck_lis_paid CHECK (paid_amount <= amount)
);
CREATE INDEX idx_lis_due ON loan_installment_schedule (due_date) WHERE status IN ('pending','partial');

-- =====================================================================
-- GP FUND  (new: GP Fund and GPF Advance were deducted but had no ledger)
-- =====================================================================
CREATE TABLE gp_fund_account (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id     uuid NOT NULL REFERENCES employee(id) UNIQUE,
  account_number  varchar UNIQUE,
  opened_on       date NOT NULL,
  closed_on       date,
  monthly_subscription numeric(14,2) NOT NULL DEFAULT 0 CHECK (monthly_subscription >= 0),
  status          record_status_enum NOT NULL DEFAULT 'active',
  created_at      timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_gpa_dates CHECK (closed_on IS NULL OR closed_on >= opened_on)
);

CREATE TABLE gp_fund_interest_rate (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  fiscal_year    varchar NOT NULL,                -- e.g. '2025-26'
  rate_percent   numeric(6,3) NOT NULL CHECK (rate_percent >= 0),
  notification_ref varchar,
  effective_from date NOT NULL,
  effective_to   date,
  CONSTRAINT ck_gir_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT ex_gir_no_overlap EXCLUDE USING gist (daterange(effective_from, effective_to, '[]') WITH &&)
);

CREATE TABLE gp_fund_transaction (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  gp_fund_account_id     uuid NOT NULL REFERENCES gp_fund_account(id),
  txn_type               gpf_txn_type_enum NOT NULL,
  txn_date               date NOT NULL,
  amount                 numeric(14,2) NOT NULL CHECK (amount <> 0),   -- signed: + credit to fund balance, - debit
  employee_loan_id       uuid REFERENCES employee_loan(id),            -- for advance / advance_recovery
  payroll_transaction_id uuid,                                         -- FK added in payroll module
  remarks                text,
  created_at             timestamptz NOT NULL DEFAULT now(),
  created_by             uuid,
  CONSTRAINT ck_gpt_sign CHECK (
    (txn_type IN ('opening','subscription','interest','advance_recovery') AND amount > 0) OR
    (txn_type IN ('advance','withdrawal','final_payment') AND amount < 0) OR
    txn_type = 'adjustment'
  )
);
CREATE INDEX idx_gpt_account_date ON gp_fund_transaction (gp_fund_account_id, txn_date);
CREATE TRIGGER trg_gpt_append_only
  BEFORE UPDATE OR DELETE ON gp_fund_transaction
  FOR EACH ROW EXECUTE FUNCTION fn_block_mutation();

CREATE VIEW v_gp_fund_balance AS
SELECT a.id AS gp_fund_account_id, a.employee_id,
       coalesce(sum(t.amount), 0)                                          AS balance,
       coalesce(sum(t.amount) FILTER (WHERE t.txn_type = 'subscription'), 0) AS total_subscribed,
       coalesce(sum(t.amount) FILTER (WHERE t.txn_type = 'interest'), 0)     AS total_interest
  FROM gp_fund_account a
  LEFT JOIN gp_fund_transaction t ON t.gp_fund_account_id = a.id
 GROUP BY a.id, a.employee_id;
-- =====================================================================
-- MODULE 14: PAYROLL ENGINE
-- =====================================================================
CREATE TABLE payroll_period (
  id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  year       int NOT NULL CHECK (year BETWEEN 2000 AND 2100),
  month      int NOT NULL CHECK (month BETWEEN 1 AND 12),
  start_date date NOT NULL,
  end_date   date NOT NULL,
  status     payroll_period_status_enum NOT NULL DEFAULT 'open',
  UNIQUE (year, month),
  CONSTRAINT ck_pperiod_dates CHECK (end_date >= start_date)
);

CREATE TABLE payroll_run (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_period_id uuid NOT NULL REFERENCES payroll_period(id),
  run_type          payroll_run_type_enum NOT NULL DEFAULT 'regular',
  run_label         varchar,                         -- e.g. 'Eid bonus 2026', 'Adhoc relief arrears 2026'
  status            payroll_run_status_enum NOT NULL DEFAULT 'draft',
  reverses_run_id   uuid REFERENCES payroll_run(id),
  prepared_by       uuid,
  reviewed_by       uuid,
  approved_by       uuid,
  approval_date     timestamptz,
  finalization_date timestamptz,
  payment_date      timestamptz,
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz,
  CONSTRAINT ck_pr_approved CHECK (status NOT IN ('approved','finalized','paid') OR approved_by IS NOT NULL),
  CONSTRAINT ck_pr_reverses CHECK (reverses_run_id IS NULL OR reverses_run_id <> id)
);
-- exactly one live REGULAR run per period; supplementary / arrears / bonus / final-settlement runs may repeat
CREATE UNIQUE INDEX uq_payroll_run_regular ON payroll_run (payroll_period_id)
  WHERE run_type = 'regular' AND status <> 'reversed';
CREATE INDEX idx_payroll_run_status ON payroll_run (status);
COMMENT ON TABLE payroll_run IS 'One live regular run per period (partial unique index); extra runs use run_type. Locked once finalized: child rows are protected by triggers. Status moves only along the allowed workflow.';

CREATE TABLE payroll_transaction (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_run_id uuid NOT NULL REFERENCES payroll_run(id),
  employee_id    uuid NOT NULL REFERENCES employee(id),
  days_payable   numeric(5,2) CHECK (days_payable IS NULL OR days_payable BETWEEN 0 AND 31),
  gross_pay      numeric(14,2) NOT NULL DEFAULT 0 CHECK (gross_pay >= 0),
  total_deductions numeric(14,2) NOT NULL DEFAULT 0 CHECK (total_deductions >= 0),
  net_payable    numeric(14,2) NOT NULL DEFAULT 0,
  status         payroll_txn_status_enum NOT NULL DEFAULT 'calculated',
  remarks        text,
  UNIQUE (payroll_run_id, employee_id),
  CONSTRAINT ck_pt_net CHECK (net_payable = gross_pay - total_deductions)
);
CREATE INDEX idx_pt_employee ON payroll_transaction (employee_id);
COMMENT ON TABLE payroll_transaction IS 'Pay-slip header: one row per employee per run. Post / grade changes inside the month are carried by payroll_transaction_segment (so proration across two posts is possible).';

CREATE TABLE payroll_transaction_segment (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id uuid NOT NULL REFERENCES payroll_transaction(id),
  post_id                uuid NOT NULL REFERENCES post(id),
  grade_id               uuid NOT NULL REFERENCES pay_scale_grade(id),
  pay_scale_stage_id     uuid REFERENCES pay_scale_stage(id),
  basic_pay              numeric(14,2) NOT NULL CHECK (basic_pay >= 0),
  period_from            date NOT NULL,
  period_to              date NOT NULL,
  days                   numeric(5,2) NOT NULL CHECK (days > 0 AND days <= 31),
  CONSTRAINT ck_pts_dates CHECK (period_to >= period_from),
  CONSTRAINT ex_pts_no_overlap EXCLUDE USING gist (
    payroll_transaction_id WITH =, daterange(period_from, period_to, '[]') WITH &&
  )
);
COMMENT ON TABLE payroll_transaction_segment IS 'One row per post/grade/stage the employee held within the pay period (mid-month transfer, promotion, acting charge ...).';

CREATE TABLE payroll_loan_deduction (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id uuid NOT NULL REFERENCES payroll_transaction(id),
  employee_loan_id       uuid NOT NULL REFERENCES employee_loan(id),
  installment_id         uuid NOT NULL,
  installment_amount     numeric(14,2) NOT NULL CHECK (installment_amount > 0),
  UNIQUE (installment_id),                         -- an installment is deducted at most once
  CONSTRAINT fk_pld_installment FOREIGN KEY (installment_id, employee_loan_id)
    REFERENCES loan_installment_schedule (id, employee_loan_id)
);

CREATE TABLE payroll_component_detail (
  id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id  uuid NOT NULL REFERENCES payroll_transaction(id),
  segment_id              uuid REFERENCES payroll_transaction_segment(id),
  salary_component_id     uuid NOT NULL REFERENCES salary_component(id),
  salary_component_rule_id uuid REFERENCES salary_component_rule(id),
  source                  component_source_enum NOT NULL DEFAULT 'rule',
  payroll_loan_deduction_id uuid UNIQUE REFERENCES payroll_loan_deduction(id),
  component_type          component_type_enum NOT NULL,
  calculation_base        varchar,
  base_amount             numeric(14,2),
  rate                    numeric(6,3),
  formula_reference       text,
  calculated_amount       numeric(14,2) NOT NULL CHECK (calculated_amount >= 0),
  notification_ref        varchar,
  effective_date          date,
  CONSTRAINT ck_pcd_loan CHECK (
    (source = 'loan') = (payroll_loan_deduction_id IS NOT NULL)
  ),
  CONSTRAINT ck_pcd_loan_type CHECK (source <> 'loan' OR component_type = 'deduction')
);
CREATE INDEX idx_pcd_txn ON payroll_component_detail (payroll_transaction_id);
COMMENT ON TABLE payroll_component_detail IS
  'One pay-slip line. Loan / advance deductions (MCA, GPF Adv) appear here ONCE with source=''loan'' and a link to payroll_loan_deduction, which links to the exact installment - so no double counting and full reconciliation.';

CREATE TABLE payroll_adjustment (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id uuid NOT NULL REFERENCES payroll_transaction(id),
  adjustment_type        adjustment_type_enum NOT NULL,
  amount                 numeric(14,2) NOT NULL CHECK (amount <> 0),   -- signed: + increases net, - decreases
  reason                 text,
  created_by             uuid,
  created_at             timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE payslip (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id uuid NOT NULL UNIQUE REFERENCES payroll_transaction(id),
  snapshot_json          jsonb NOT NULL,
  generated_at           timestamptz NOT NULL DEFAULT now(),
  file_reference         varchar
);

-- Income-tax ledger (year-to-date withholding for annualised tax)
CREATE TABLE employee_tax_ledger (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id            uuid NOT NULL REFERENCES employee(id),
  tax_year_id            uuid NOT NULL REFERENCES tax_year(id),
  payroll_transaction_id uuid NOT NULL UNIQUE REFERENCES payroll_transaction(id),
  taxable_income         numeric(14,2) NOT NULL CHECK (taxable_income >= 0),
  tax_withheld           numeric(14,2) NOT NULL CHECK (tax_withheld >= 0),
  created_at             timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_etl_emp_year ON employee_tax_ledger (employee_id, tax_year_id);

CREATE VIEW v_employee_tax_ytd AS
SELECT l.employee_id, l.tax_year_id,
       sum(l.taxable_income) AS taxable_income_ytd,
       sum(l.tax_withheld)   AS tax_withheld_ytd
  FROM employee_tax_ledger l
  JOIN payroll_transaction t ON t.id = l.payroll_transaction_id
  JOIN payroll_run r ON r.id = t.payroll_run_id
 WHERE r.status <> 'reversed'
 GROUP BY l.employee_id, l.tax_year_id;

-- cross-module FKs that needed payroll_transaction to exist
ALTER TABLE employee_separation
  ADD CONSTRAINT fk_sep_settlement FOREIGN KEY (settlement_payroll_transaction_id) REFERENCES payroll_transaction(id);
ALTER TABLE gp_fund_transaction
  ADD CONSTRAINT fk_gpt_payroll FOREIGN KEY (payroll_transaction_id) REFERENCES payroll_transaction(id);

-- =====================================================================
-- MODULE 15: BANK / PAYMENT
-- =====================================================================
CREATE TABLE payroll_payment (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  payroll_transaction_id uuid NOT NULL REFERENCES payroll_transaction(id),
  employee_id            uuid NOT NULL REFERENCES employee(id),
  bank_account_id        uuid NOT NULL,
  -- snapshot of the destination at payment time (bank record may later change)
  bank_name_snapshot       varchar,
  branch_name_snapshot     varchar,
  account_number_snapshot  varchar,
  iban_snapshot            varchar,
  payment_method         payment_method_enum NOT NULL DEFAULT 'bank_transfer',
  payment_status         payment_status_enum NOT NULL DEFAULT 'pending',
  payment_date           date,
  payment_reference      varchar,
  created_at             timestamptz NOT NULL DEFAULT now(),
  -- the account must belong to the same employee as the payment
  CONSTRAINT fk_pp_bank_owner FOREIGN KEY (bank_account_id, employee_id)
    REFERENCES employee_bank_account (id, employee_id),
  CONSTRAINT ck_pp_processed CHECK (payment_status <> 'processed' OR (payment_date IS NOT NULL AND payment_reference IS NOT NULL))
);
CREATE UNIQUE INDEX uq_pp_one_live ON payroll_payment (payroll_transaction_id) WHERE payment_status IN ('pending','processed');
CREATE INDEX idx_pp_bank ON payroll_payment (bank_account_id);
COMMENT ON TABLE payroll_payment IS 'Restricted-access (holds bank details). Destination snapshot is filled by trigger on insert; composite FK guarantees the account belongs to the paid employee.';

CREATE OR REPLACE FUNCTION fn_payment_prepare() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_emp uuid; b employee_bank_account%ROWTYPE;
BEGIN
  SELECT employee_id INTO v_emp FROM payroll_transaction WHERE id = NEW.payroll_transaction_id;
  IF v_emp IS DISTINCT FROM NEW.employee_id THEN
    RAISE EXCEPTION 'payment employee % does not match payroll transaction employee %', NEW.employee_id, v_emp;
  END IF;
  IF TG_OP = 'INSERT' THEN
    SELECT * INTO b FROM employee_bank_account WHERE id = NEW.bank_account_id;
    NEW.bank_name_snapshot      := b.bank_name;
    NEW.branch_name_snapshot    := b.branch_name;
    NEW.account_number_snapshot := b.account_number;
    NEW.iban_snapshot           := b.iban;
  ELSIF NEW.bank_account_id <> OLD.bank_account_id
     OR NEW.iban_snapshot IS DISTINCT FROM OLD.iban_snapshot
     OR NEW.account_number_snapshot IS DISTINCT FROM OLD.account_number_snapshot THEN
    RAISE EXCEPTION 'payment destination is immutable once created; create a new payment row';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_payment_prepare
  BEFORE INSERT OR UPDATE ON payroll_payment
  FOR EACH ROW EXECUTE FUNCTION fn_payment_prepare();

-- =====================================================================
-- PAYROLL INTEGRITY TRIGGERS
-- =====================================================================

-- 1) status workflow of a run
CREATE OR REPLACE FUNCTION fn_payroll_run_transition() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.status = OLD.status THEN
    IF OLD.status IN ('finalized','paid','reversed') AND
       (NEW.payroll_period_id <> OLD.payroll_period_id OR NEW.run_type <> OLD.run_type) THEN
      RAISE EXCEPTION 'payroll_run % is locked (%)', OLD.id, OLD.status;
    END IF;
    RETURN NEW;
  END IF;

  IF NOT (
       (OLD.status = 'draft'      AND NEW.status = 'calculated') OR
       (OLD.status = 'calculated' AND NEW.status IN ('draft','reviewed')) OR
       (OLD.status = 'reviewed'   AND NEW.status IN ('calculated','approved')) OR
       (OLD.status = 'approved'   AND NEW.status IN ('reviewed','finalized')) OR
       (OLD.status = 'finalized'  AND NEW.status IN ('paid','reversed')) OR
       (OLD.status = 'paid'       AND NEW.status = 'reversed')
  ) THEN
    RAISE EXCEPTION 'illegal payroll_run status change % -> %', OLD.status, NEW.status;
  END IF;

  IF NEW.status = 'approved'  AND NEW.approval_date     IS NULL THEN NEW.approval_date     := now(); END IF;
  IF NEW.status = 'finalized' AND NEW.finalization_date IS NULL THEN NEW.finalization_date := now(); END IF;
  IF NEW.status = 'paid'      AND NEW.payment_date      IS NULL THEN NEW.payment_date      := now(); END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_payroll_run_transition
  BEFORE UPDATE ON payroll_run
  FOR EACH ROW EXECUTE FUNCTION fn_payroll_run_transition();

-- 2) once a run is finalized / paid / reversed its figures are immutable
CREATE OR REPLACE FUNCTION fn_block_if_run_locked() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_run uuid; v_status payroll_run_status_enum; r record;
BEGIN
  r := CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;

  IF TG_TABLE_NAME = 'payroll_transaction' THEN
    v_run := r.payroll_run_id;
  ELSIF TG_TABLE_NAME = 'payroll_loan_deduction' OR TG_TABLE_NAME = 'payroll_transaction_segment'
     OR TG_TABLE_NAME = 'payroll_component_detail' OR TG_TABLE_NAME = 'payroll_adjustment'
     OR TG_TABLE_NAME = 'employee_tax_ledger' THEN
    SELECT payroll_run_id INTO v_run FROM payroll_transaction WHERE id = r.payroll_transaction_id;
  END IF;

  SELECT status INTO v_status FROM payroll_run WHERE id = v_run;

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

CREATE TRIGGER trg_lock_pt   BEFORE INSERT OR UPDATE OR DELETE ON payroll_transaction
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();
CREATE TRIGGER trg_lock_pts  BEFORE INSERT OR UPDATE OR DELETE ON payroll_transaction_segment
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();
CREATE TRIGGER trg_lock_pcd  BEFORE INSERT OR UPDATE OR DELETE ON payroll_component_detail
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();
CREATE TRIGGER trg_lock_padj BEFORE INSERT OR UPDATE OR DELETE ON payroll_adjustment
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();
CREATE TRIGGER trg_lock_pld  BEFORE INSERT OR UPDATE OR DELETE ON payroll_loan_deduction
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();
CREATE TRIGGER trg_lock_etl  BEFORE INSERT OR UPDATE OR DELETE ON employee_tax_ledger
  FOR EACH ROW EXECUTE FUNCTION fn_block_if_run_locked();

-- the payslip snapshot never changes (file_reference may be filled in later)
CREATE OR REPLACE FUNCTION fn_payslip_guard() RETURNS trigger
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
CREATE TRIGGER trg_payslip_guard BEFORE UPDATE OR DELETE ON payslip
  FOR EACH ROW EXECUTE FUNCTION fn_payslip_guard();

-- 3) finalizing a run posts loan installments (and GPF advance recoveries); reversing undoes it
CREATE OR REPLACE FUNCTION fn_payroll_apply_loans() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE d record; v_dir int; v_acct uuid;
BEGIN
  IF NEW.status = OLD.status THEN RETURN NEW; END IF;

  IF NEW.status = 'finalized' THEN v_dir := 1;
  ELSIF NEW.status = 'reversed' AND OLD.status IN ('finalized','paid') THEN v_dir := -1;
  ELSE RETURN NEW;
  END IF;

  FOR d IN
    SELECT ld.id, ld.installment_id, ld.employee_loan_id, ld.installment_amount,
           t.id AS txn_id, t.employee_id, lt.is_gpf_advance
      FROM payroll_loan_deduction ld
      JOIN payroll_transaction t ON t.id = ld.payroll_transaction_id
      JOIN employee_loan l ON l.id = ld.employee_loan_id
      JOIN loan_type lt ON lt.id = l.loan_type_id
     WHERE t.payroll_run_id = NEW.id
  LOOP
    UPDATE loan_installment_schedule s
       SET paid_amount = s.paid_amount + v_dir * d.installment_amount,
           status = CASE
                      WHEN s.paid_amount + v_dir * d.installment_amount >= s.amount THEN 'paid'::installment_status_enum
                      WHEN s.paid_amount + v_dir * d.installment_amount > 0         THEN 'partial'::installment_status_enum
                      ELSE 'pending'::installment_status_enum
                    END
     WHERE s.id = d.installment_id;

    UPDATE employee_loan l
       SET remaining_balance = greatest(0, l.remaining_balance - v_dir * d.installment_amount),
           status = CASE
                      WHEN greatest(0, l.remaining_balance - v_dir * d.installment_amount) = 0 THEN 'completed'::loan_status_enum
                      ELSE 'active'::loan_status_enum
                    END,
           updated_at = now()
     WHERE l.id = d.employee_loan_id;

    IF d.is_gpf_advance THEN
      SELECT id INTO v_acct FROM gp_fund_account WHERE employee_id = d.employee_id;
      IF v_acct IS NOT NULL THEN
        INSERT INTO gp_fund_transaction
          (gp_fund_account_id, txn_type, txn_date, amount, employee_loan_id, payroll_transaction_id, remarks)
        VALUES (v_acct,
                CASE WHEN v_dir = 1 THEN 'advance_recovery'::gpf_txn_type_enum ELSE 'adjustment'::gpf_txn_type_enum END,
                current_date, v_dir * d.installment_amount, d.employee_loan_id, d.txn_id,
                CASE WHEN v_dir = 1 THEN 'recovered via payroll' ELSE 'reversal of payroll recovery' END);
      END IF;
    END IF;
  END LOOP;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_payroll_apply_loans
  AFTER UPDATE OF status ON payroll_run
  FOR EACH ROW EXECUTE FUNCTION fn_payroll_apply_loans();

-- =====================================================================
-- MODULE 16: WORK / TASK ASSIGNMENT
-- =====================================================================
CREATE TABLE employee_task (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id         uuid NOT NULL REFERENCES employee(id),
  assigned_by         uuid NOT NULL,         -- platform users.id
  title               varchar NOT NULL,
  description         text,
  priority            task_priority_enum NOT NULL DEFAULT 'medium',
  due_date            date,
  status              task_status_enum NOT NULL DEFAULT 'pending',
  progress_percentage int NOT NULL DEFAULT 0 CHECK (progress_percentage BETWEEN 0 AND 100),
  completed_at        timestamptz,
  created_at          timestamptz NOT NULL DEFAULT now(),
  updated_at          timestamptz,
  CONSTRAINT ck_et_completed CHECK ((status = 'completed') = (completed_at IS NOT NULL))
);
CREATE INDEX idx_et_employee ON employee_task (employee_id);
CREATE INDEX idx_et_status   ON employee_task (status);
CREATE INDEX idx_et_due      ON employee_task (due_date);
COMMENT ON TABLE employee_task IS 'Lightweight task assignment for the Employee Portal. progress_percentage is maintained BY TRIGGER from employee_task_update.';

CREATE TABLE employee_task_update (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  task_id             uuid NOT NULL REFERENCES employee_task(id),
  progress_percentage int NOT NULL CHECK (progress_percentage BETWEEN 0 AND 100),
  notes               text,
  updated_by          uuid NOT NULL,
  created_at          timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX idx_etu_task ON employee_task_update (task_id, created_at);
CREATE TRIGGER trg_etu_append_only BEFORE UPDATE OR DELETE ON employee_task_update
  FOR EACH ROW EXECUTE FUNCTION fn_block_mutation();

CREATE OR REPLACE FUNCTION fn_task_mirror_progress() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  UPDATE employee_task
     SET progress_percentage = NEW.progress_percentage,
         status = CASE
                    WHEN NEW.progress_percentage = 100 THEN 'completed'::task_status_enum
                    WHEN status = 'pending' AND NEW.progress_percentage > 0 THEN 'in_progress'::task_status_enum
                    ELSE status
                  END,
         completed_at = CASE WHEN NEW.progress_percentage = 100 THEN coalesce(completed_at, now()) ELSE completed_at END,
         updated_at = now()
   WHERE id = NEW.task_id AND status NOT IN ('cancelled');
  RETURN NEW;
END $$;
CREATE TRIGGER trg_etu_mirror AFTER INSERT ON employee_task_update
  FOR EACH ROW EXECUTE FUNCTION fn_task_mirror_progress();

-- =====================================================================
-- MODULE 17: EMPLOYEE REQUESTS
-- =====================================================================
CREATE TABLE employee_request_type (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name              varchar NOT NULL UNIQUE,
  code              varchar UNIQUE,
  requires_document boolean NOT NULL DEFAULT false,
  is_active         boolean NOT NULL DEFAULT true
);

CREATE TABLE employee_request (
  id                     uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  employee_id            uuid NOT NULL REFERENCES employee(id),
  request_type_id        uuid NOT NULL REFERENCES employee_request_type(id),
  subject                varchar,
  description            text,
  requested_data         jsonb,
  supporting_document_id uuid REFERENCES employee_document(id),
  status                 employee_request_status_enum NOT NULL DEFAULT 'pending',
  submitted_at           timestamptz NOT NULL DEFAULT now(),
  reviewed_by            uuid,
  reviewed_at            timestamptz,
  remarks                text,
  CONSTRAINT ck_er_review CHECK (status NOT IN ('approved','rejected') OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE INDEX idx_er_employee ON employee_request (employee_id);
CREATE INDEX idx_er_type     ON employee_request (request_type_id);
CREATE INDEX idx_er_status   ON employee_request (status);

CREATE OR REPLACE FUNCTION fn_request_validate() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_req boolean; v_doc_emp uuid;
BEGIN
  SELECT requires_document INTO v_req FROM employee_request_type WHERE id = NEW.request_type_id;
  IF v_req AND NEW.supporting_document_id IS NULL THEN
    RAISE EXCEPTION 'this request type requires a supporting document';
  END IF;
  IF NEW.supporting_document_id IS NOT NULL THEN
    SELECT employee_id INTO v_doc_emp FROM employee_document WHERE id = NEW.supporting_document_id;
    IF v_doc_emp IS DISTINCT FROM NEW.employee_id THEN
      RAISE EXCEPTION 'supporting document belongs to a different employee';
    END IF;
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER trg_request_validate BEFORE INSERT OR UPDATE ON employee_request
  FOR EACH ROW EXECUTE FUNCTION fn_request_validate();
-- =====================================================================
-- FINISHING: generic triggers, FK indexes, security, seed data
-- =====================================================================

-- 1) keep updated_at current on every table that has the column
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

-- 2) PostgreSQL does not index foreign keys automatically: create an index for every FK
--    whose columns are not already the leading columns of some index.
DO $$
DECLARE f record; v_name text;
BEGIN
  FOR f IN
    SELECT c.conrelid, c.conrelid::regclass AS tbl, c.conkey, c.conname,
           (SELECT string_agg(a.attname, ', ' ORDER BY k.ord)
              FROM unnest(c.conkey) WITH ORDINALITY k(attnum, ord)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum) AS cols,
           (SELECT string_agg(a.attname, '_' ORDER BY k.ord)
              FROM unnest(c.conkey) WITH ORDINALITY k(attnum, ord)
              JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum) AS colname
      FROM pg_constraint c
      JOIN pg_namespace n ON n.oid = c.connamespace AND n.nspname = 'hrms'
     WHERE c.contype = 'f'
  LOOP
    IF NOT EXISTS (
      SELECT 1 FROM pg_index i
       WHERE i.indrelid = f.conrelid AND i.indisvalid
         AND (i.indkey::int2[])[0:array_length(f.conkey,1)-1] = f.conkey
    ) THEN
      v_name := left('idx_' || (SELECT relname FROM pg_class WHERE oid = f.conrelid) || '_' || f.colname, 63);
      EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %s (%s)', v_name, f.tbl, f.cols);
    END IF;
  END LOOP;
END $$;

-- 3) access roles (NOLOGIN group roles: grant them to your real login roles)
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hrms_readonly') THEN CREATE ROLE hrms_readonly NOLOGIN; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hrms_hr')       THEN CREATE ROLE hrms_hr       NOLOGIN; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'hrms_payroll')  THEN CREATE ROLE hrms_payroll  NOLOGIN; END IF;
END $$;

GRANT USAGE ON SCHEMA hrms TO hrms_readonly, hrms_hr, hrms_payroll;
GRANT SELECT ON ALL TABLES IN SCHEMA hrms TO hrms_hr, hrms_payroll;
GRANT INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA hrms TO hrms_hr, hrms_payroll;

-- general reporting role: everything except bank / payment tables, and employee without CNIC
GRANT SELECT ON ALL TABLES IN SCHEMA hrms TO hrms_readonly;
REVOKE SELECT ON employee_bank_account, payroll_payment FROM hrms_readonly;
REVOKE SELECT ON employee FROM hrms_readonly;
GRANT SELECT (id, employee_number, employment_type, employment_method, first_name, middle_name,
              last_name, full_name, date_of_birth, gender, marital_status, profile_status,
              employment_status, created_at)
  ON employee TO hrms_readonly;

-- HR staff do not touch bank details or run payroll; payroll staff do not edit service records
REVOKE ALL ON employee_bank_account, payroll_payment FROM hrms_hr;
REVOKE INSERT, UPDATE, DELETE ON payroll_run, payroll_transaction, payroll_transaction_segment,
  payroll_component_detail, payroll_adjustment, payroll_loan_deduction, payslip, employee_tax_ledger FROM hrms_hr;
REVOKE INSERT, UPDATE, DELETE ON employee_service_history, position_assignment, hr_action_request,
  employee_separation, post, post_version, organization_unit, organization_unit_version FROM hrms_payroll;

-- 4) seed catalogues (safe to re-run)
INSERT INTO organization_unit_type (name, code, hierarchy_level) VALUES
  ('Authority','AUTH',0), ('Department','DEPT',1), ('Wing','WING',2), ('Division','DIV',3),
  ('Section','SEC',4), ('Office','OFF',5)
ON CONFLICT DO NOTHING;

INSERT INTO pay_scale_grade (bps_number, grade_name)
SELECT g, 'BPS-' || g FROM generate_series(1,22) g
ON CONFLICT DO NOTHING;

INSERT INTO service_event_type (name, category) VALUES
  ('Appointment','entry'), ('Joining','entry'), ('Transfer','movement'), ('Promotion','movement'),
  ('Demotion','movement'), ('Deputation In','movement'), ('Deputation Out','movement'),
  ('Regularization','status'), ('LWOP','status'), ('Suspension','disciplinary'),
  ('Reinstatement','disciplinary'), ('Retirement','exit'), ('Resignation','exit'),
  ('Termination','exit'), ('Death','exit')
ON CONFLICT DO NOTHING;

INSERT INTO recruitment_method (name) VALUES
  ('Project Regularization'), ('Board of Authority'), ('Public Service Commission (CSS/PMS etc.)')
ON CONFLICT DO NOTHING;

INSERT INTO document_type (name, requires_expiry) VALUES
  ('CNIC', true), ('Domicile', false), ('Appointment Order', false), ('Joining Report', false),
  ('Degree / Certificate', false), ('Service Book', false), ('Medical Certificate', true),
  ('NOC', false), ('Other', false)
ON CONFLICT DO NOTHING;

INSERT INTO salary_component (component_code, component_name, component_type, is_taxable) VALUES
  ('BASIC',      'Basic Pay',                'earning',   true),
  ('GDA_AUTH',   'GDA Authority Allowance',  'earning',   true),
  ('MEDICAL',    'Medical Allowance',        'earning',   true),
  ('HOUSING',    'Housing Subsidy',          'earning',   true),
  ('UTILITY',    'Utility Allowance',        'earning',   true),
  ('ADHOC_2022', 'Adhoc Relief Allowance 2022','earning', true),
  ('ADHOC_2023', 'Adhoc Relief Allowance 2023','earning', true),
  ('ADHOC_2024', 'Adhoc Relief Allowance 2024','earning', true),
  ('ADHOC_2025', 'Adhoc Relief Allowance 2025','earning', true),
  ('ADHOC_2026', 'Adhoc Relief Allowance 2026','earning', true),
  ('BF',         'Benevolent Fund (BF)',     'deduction', false),
  ('GPF',        'GP Fund',                  'deduction', false),
  ('MCA',        'Motor Car Advance (MCA)',  'deduction', false),
  ('GPF_ADV',    'GP Fund Advance',          'deduction', false),
  ('RBD',        'Rent & Building Dues (RB&D)','deduction', false),
  ('IT',         'Income Tax (I.T.)',        'deduction', false)
ON CONFLICT DO NOTHING;

INSERT INTO loan_type (name, salary_component_id, is_gpf_advance)
SELECT 'Motor Car Advance (MCA)', id, false FROM salary_component WHERE component_code = 'MCA'
ON CONFLICT DO NOTHING;
INSERT INTO loan_type (name, salary_component_id, is_gpf_advance)
SELECT 'GP Fund Advance', id, true FROM salary_component WHERE component_code = 'GPF_ADV'
ON CONFLICT DO NOTHING;

INSERT INTO leave_type (name, is_paid, max_days_per_year, carry_forward_allowed, affects_payroll) VALUES
  ('Casual Leave', true, 20, false, false),
  ('Earned Leave', true, NULL, true, false),
  ('Medical Leave', true, NULL, false, false),
  ('Leave Without Pay (LWOP)', false, NULL, false, true)
ON CONFLICT DO NOTHING;

INSERT INTO employee_request_type (name, code, requires_document) VALUES
  ('Transfer Request','TRANSFER',false),
  ('Document Request','DOCUMENT',false),
  ('Profile Correction Request','PROFILE_FIX',true)
ON CONFLICT DO NOTHING;
