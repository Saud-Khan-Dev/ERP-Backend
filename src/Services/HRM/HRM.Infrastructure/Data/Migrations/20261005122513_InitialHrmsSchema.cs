using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRM.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialHrmsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hrms");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:hrms.address_type_enum", "permanent,current,mailing,other")
                .Annotation("Npgsql:Enum:hrms.adjustment_type_enum", "arrears,recovery,bonus,correction,other")
                .Annotation("Npgsql:Enum:hrms.assignment_type_enum", "regular,acting,additional_charge,look_after")
                .Annotation("Npgsql:Enum:hrms.attendance_status_enum", "present,absent,late,half_day,on_leave,official_duty,holiday,weekend")
                .Annotation("Npgsql:Enum:hrms.calculation_method_enum", "fixed,percentage,formula,tiered")
                .Annotation("Npgsql:Enum:hrms.component_source_enum", "rule,override,loan,gpf,tax,adjustment,manual")
                .Annotation("Npgsql:Enum:hrms.component_type_enum", "earning,deduction")
                .Annotation("Npgsql:Enum:hrms.contact_type_enum", "mobile,phone,email,whatsapp,other")
                .Annotation("Npgsql:Enum:hrms.employee_request_status_enum", "pending,approved,rejected,cancelled")
                .Annotation("Npgsql:Enum:hrms.employment_method_enum", "scheduled_seat,gda_personal")
                .Annotation("Npgsql:Enum:hrms.employment_status_enum", "active,on_leave,suspended,deputed_out,retired,resigned,terminated,deceased")
                .Annotation("Npgsql:Enum:hrms.employment_type_enum", "regular,contract,project_based,deputation_in,adhoc,daily_wage")
                .Annotation("Npgsql:Enum:hrms.gender_enum", "male,female,other")
                .Annotation("Npgsql:Enum:hrms.gpf_txn_type_enum", "opening,subscription,interest,advance,advance_recovery,withdrawal,final_payment,adjustment")
                .Annotation("Npgsql:Enum:hrms.holiday_type_enum", "public,religious,provincial,optional")
                .Annotation("Npgsql:Enum:hrms.hr_action_status_enum", "draft,pending,approved,rejected,cancelled,applied")
                .Annotation("Npgsql:Enum:hrms.hr_action_type_enum", "appointment,joining,transfer,promotion,demotion,deputation_in,deputation_out,regularization,lwop,suspension,reinstatement,retirement,resignation,termination,death,other")
                .Annotation("Npgsql:Enum:hrms.installment_status_enum", "pending,partial,paid,waived")
                .Annotation("Npgsql:Enum:hrms.leave_status_enum", "pending,approved,rejected,cancelled")
                .Annotation("Npgsql:Enum:hrms.leave_txn_type_enum", "opening,accrual,usage,usage_reversal,carry_forward,lapse,encashment,adjustment")
                .Annotation("Npgsql:Enum:hrms.loan_status_enum", "active,completed,cancelled,written_off")
                .Annotation("Npgsql:Enum:hrms.marital_status_enum", "single,married,divorced,widowed")
                .Annotation("Npgsql:Enum:hrms.payment_method_enum", "bank_transfer,cheque,cash")
                .Annotation("Npgsql:Enum:hrms.payment_status_enum", "pending,processed,failed,returned")
                .Annotation("Npgsql:Enum:hrms.payroll_period_status_enum", "open,closed,locked")
                .Annotation("Npgsql:Enum:hrms.payroll_run_status_enum", "draft,calculated,reviewed,approved,finalized,paid,reversed")
                .Annotation("Npgsql:Enum:hrms.payroll_run_type_enum", "regular,supplementary,arrears,bonus,final_settlement")
                .Annotation("Npgsql:Enum:hrms.payroll_txn_status_enum", "calculated,held,released")
                .Annotation("Npgsql:Enum:hrms.position_status_enum", "sanctioned,vacant,partially_filled,filled,abolished,frozen")
                .Annotation("Npgsql:Enum:hrms.post_lifecycle_enum", "sanctioned,frozen,abolished")
                .Annotation("Npgsql:Enum:hrms.qualification_level_enum", "matric,intermediate,bachelors,masters,mphil,phd,other")
                .Annotation("Npgsql:Enum:hrms.record_status_enum", "active,inactive")
                .Annotation("Npgsql:Enum:hrms.review_status_enum", "draft,submitted,acknowledged,finalized")
                .Annotation("Npgsql:Enum:hrms.separation_type_enum", "retirement,resignation,termination,dismissal,death,end_of_contract,deputation_end,other")
                .Annotation("Npgsql:Enum:hrms.task_priority_enum", "low,medium,high,urgent")
                .Annotation("Npgsql:Enum:hrms.task_status_enum", "pending,in_progress,completed,on_hold,cancelled")
                .Annotation("Npgsql:Enum:hrms.verification_status_enum", "unverified,pending,verified,rejected")
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "designation",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "varchar", nullable: false),
                    code = table.Column<string>(type: "varchar", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("designation_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    requires_expiry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("document_type_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employee",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_number = table.Column<string>(type: "varchar", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employment_type = table.Column<EmploymentType>(type: "hrms.employment_type_enum", nullable: false),
                    employment_method = table.Column<EmploymentMethod>(type: "hrms.employment_method_enum", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "varchar", nullable: false),
                    middle_name = table.Column<string>(type: "varchar", nullable: true),
                    last_name = table.Column<string>(type: "varchar", nullable: true),
                    full_name = table.Column<string>(type: "varchar", nullable: true, computedColumnSql: "first_name || coalesce(' ' || middle_name, '') || coalesce(' ' || last_name, '')", stored: true),
                    cnic = table.Column<string>(type: "varchar", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<Gender>(type: "hrms.gender_enum", nullable: true),
                    nationality = table.Column<string>(type: "varchar", nullable: true, defaultValue: "Pakistani"),
                    marital_status = table.Column<MaritalStatus>(type: "hrms.marital_status_enum", nullable: true),
                    blood_group = table.Column<string>(type: "varchar", nullable: true),
                    profile_status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    employment_status = table.Column<EmploymentStatus>(type: "hrms.employment_status_enum", nullable: false, defaultValue: EmploymentStatus.Active),
                    photo_reference = table.Column<string>(type: "varchar", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_pkey", x => x.id);
                    table.CheckConstraint("ck_emp_blood", "blood_group IS NULL OR blood_group IN ('A+','A-','B+','B-','AB+','AB-','O+','O-')");
                    table.CheckConstraint("ck_emp_cnic", "cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'");
                    table.CheckConstraint("ck_emp_dob", "date_of_birth IS NULL OR date_of_birth < current_date");
                    table.CheckConstraint("ck_emp_method", "(employment_type = 'regular' AND employment_method IS NOT NULL) OR (employment_type <> 'regular' AND employment_method IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "employee_request_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    code = table.Column<string>(type: "varchar", nullable: true),
                    requires_document = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_request_type_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gp_fund_interest_rate",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fiscal_year = table.Column<string>(type: "varchar", nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gp_fund_interest_rate_pkey", x => x.id);
                    table.CheckConstraint("ck_gir_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("gp_fund_interest_rate_rate_percent_check", "rate_percent >= 0");
                });

            migrationBuilder.CreateTable(
                name: "leave_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    max_days_per_year = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    accrual_rule = table.Column<string>(type: "text", nullable: true),
                    carry_forward_allowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    affects_payroll = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("leave_type_pkey", x => x.id);
                    table.CheckConstraint("leave_type_max_days_per_year_check", "max_days_per_year IS NULL OR max_days_per_year >= 0");
                });

            migrationBuilder.CreateTable(
                name: "location",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    address_line = table.Column<string>(type: "varchar", nullable: true),
                    city = table.Column<string>(type: "varchar", nullable: true),
                    district = table.Column<string>(type: "varchar", nullable: true),
                    province = table.Column<string>(type: "varchar", nullable: true, defaultValue: "Khyber Pakhtunkhwa"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("location_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "varchar", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_unit_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    code = table.Column<string>(type: "varchar", nullable: false),
                    hierarchy_level = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_unit_type_pkey", x => x.id);
                    table.CheckConstraint("organization_unit_type_hierarchy_level_check", "hierarchy_level IS NULL OR hierarchy_level >= 0");
                });

            migrationBuilder.CreateTable(
                name: "pay_scale_grade",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    bps_number = table.Column<int>(type: "integer", nullable: false),
                    grade_name = table.Column<string>(type: "varchar", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pay_scale_grade_pkey", x => x.id);
                    table.CheckConstraint("pay_scale_grade_bps_number_check", "bps_number >= 1 AND bps_number <= 22");
                });

            migrationBuilder.CreateTable(
                name: "payroll_period",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<PayrollPeriodStatus>(type: "hrms.payroll_period_status_enum", nullable: false, defaultValue: PayrollPeriodStatus.Open)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_period_pkey", x => x.id);
                    table.CheckConstraint("ck_pperiod_dates", "end_date >= start_date");
                    table.CheckConstraint("payroll_period_month_check", "month BETWEEN 1 AND 12");
                    table.CheckConstraint("payroll_period_year_check", "year BETWEEN 2000 AND 2100");
                });

            migrationBuilder.CreateTable(
                name: "performance_period",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_period_pkey", x => x.id);
                    table.CheckConstraint("ck_pp_dates", "end_date >= start_date");
                });

            migrationBuilder.CreateTable(
                name: "post",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_code = table.Column<string>(type: "varchar", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("post_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recruitment_method",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("recruitment_method_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_component",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    component_code = table.Column<string>(type: "varchar", nullable: false),
                    component_name = table.Column<string>(type: "varchar", nullable: false),
                    component_type = table.Column<ComponentType>(type: "hrms.component_type_enum", nullable: false),
                    is_taxable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("salary_component_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_event_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    category = table.Column<string>(type: "varchar", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("service_event_type_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_year",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    year_label = table.Column<string>(type: "varchar", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_year_pkey", x => x.id);
                    table.CheckConstraint("ck_ty_dates", "end_date > start_date");
                });

            migrationBuilder.CreateTable(
                name: "work_shift",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    grace_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    working_weekdays = table.Column<short[]>(type: "smallint[]", nullable: false, defaultValueSql: "'{1,2,3,4,5}'::smallint[]"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("work_shift_pkey", x => x.id);
                    table.CheckConstraint("ck_ws_days", "working_weekdays <@ ARRAY[1,2,3,4,5,6,7]::smallint[] AND cardinality(working_weekdays) > 0");
                    table.CheckConstraint("work_shift_grace_minutes_check", "grace_minutes >= 0");
                });

            migrationBuilder.CreateTable(
                name: "employee_address",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_type = table.Column<AddressType>(type: "hrms.address_type_enum", nullable: false),
                    address_line = table.Column<string>(type: "varchar", nullable: true),
                    city = table.Column<string>(type: "varchar", nullable: true),
                    district = table.Column<string>(type: "varchar", nullable: true),
                    province = table.Column<string>(type: "varchar", nullable: true),
                    postal_code = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_address_pkey", x => x.id);
                    table.ForeignKey(
                        name: "employee_address_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_bank_account",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "varchar", nullable: false),
                    branch_name = table.Column<string>(type: "varchar", nullable: true),
                    account_number = table.Column<string>(type: "varchar", nullable: true),
                    iban = table.Column<string>(type: "varchar", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_bank_account_pkey", x => x.id);
                    table.UniqueConstraint("employee_bank_account_id_employee_id_key", x => new { x.id, x.employee_id });
                    table.CheckConstraint("ck_eba_iban", "iban IS NULL OR iban ~ '^PK[0-9]{2}[A-Z]{4}[0-9]{16}$'");
                    table.CheckConstraint("ck_eba_ident", "account_number IS NOT NULL OR iban IS NOT NULL");
                    table.ForeignKey(
                        name: "employee_bank_account_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_contact",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_type = table.Column<ContactType>(type: "hrms.contact_type_enum", nullable: false),
                    value = table.Column<string>(type: "varchar", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_contact_pkey", x => x.id);
                    table.ForeignKey(
                        name: "employee_contact_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_document",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "varchar", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    file_reference = table.Column<string>(type: "varchar", nullable: false),
                    verification_status = table.Column<VerificationStatus>(type: "hrms.verification_status_enum", nullable: false, defaultValue: VerificationStatus.Unverified),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_document_pkey", x => x.id);
                    table.CheckConstraint("ck_ed_dates", "expiry_date IS NULL OR issue_date IS NULL OR expiry_date >= issue_date");
                    table.CheckConstraint("ck_ed_verified", "verification_status <> 'verified' OR (verified_by IS NOT NULL AND verification_date IS NOT NULL)");
                    table.ForeignKey(
                        name: "employee_document_document_type_id_fkey",
                        column: x => x.document_type_id,
                        principalSchema: "hrms",
                        principalTable: "document_type",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_document_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_education",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_level = table.Column<QualificationLevel>(type: "hrms.qualification_level_enum", nullable: false),
                    degree_title = table.Column<string>(type: "varchar", nullable: false),
                    institution_name = table.Column<string>(type: "varchar", nullable: false),
                    board_or_university = table.Column<string>(type: "varchar", nullable: true),
                    passing_year = table.Column<int>(type: "integer", nullable: true),
                    grade_or_cgpa = table.Column<string>(type: "varchar", nullable: true),
                    is_highest_qualification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    file_reference = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_education_pkey", x => x.id);
                    table.CheckConstraint("employee_education_passing_year_check", "passing_year IS NULL OR passing_year BETWEEN 1950 AND 2100");
                    table.ForeignKey(
                        name: "employee_education_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_emergency_contact",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    relation = table.Column<string>(type: "varchar", nullable: true),
                    phone = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_emergency_contact_pkey", x => x.id);
                    table.ForeignKey(
                        name: "employee_emergency_contact_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_family_member",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "varchar", nullable: true),
                    name = table.Column<string>(type: "varchar", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    cnic = table.Column<string>(type: "varchar", nullable: true),
                    occupation = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_family_member_pkey", x => x.id);
                    table.CheckConstraint("employee_family_member_cnic_check", "cnic IS NULL OR cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'");
                    table.ForeignKey(
                        name: "employee_family_member_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_task",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "varchar", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<TaskPriority>(type: "hrms.task_priority_enum", nullable: false, defaultValue: TaskPriority.Medium),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<EmployeeTaskStatus>(type: "hrms.task_status_enum", nullable: false, defaultValue: EmployeeTaskStatus.Pending),
                    progress_percentage = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_task_pkey", x => x.id);
                    table.CheckConstraint("ck_et_completed", "(status = 'completed') = (completed_at IS NOT NULL)");
                    table.CheckConstraint("employee_task_progress_percentage_check", "progress_percentage BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "employee_task_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "gp_fund_account",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_number = table.Column<string>(type: "varchar", nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    monthly_subscription = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("gp_fund_account_pkey", x => x.id);
                    table.CheckConstraint("ck_gpa_dates", "closed_on IS NULL OR closed_on >= opened_on");
                    table.CheckConstraint("gp_fund_account_monthly_subscription_check", "monthly_subscription >= 0");
                    table.ForeignKey(
                        name: "gp_fund_account_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "leave_application",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    status = table.Column<LeaveStatus>(type: "hrms.leave_status_enum", nullable: false, defaultValue: LeaveStatus.Pending),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("leave_application_pkey", x => x.id);
                    table.CheckConstraint("ck_la_approval", "status <> 'approved' OR (approved_by IS NOT NULL AND approval_date IS NOT NULL)");
                    table.CheckConstraint("ck_la_dates", "end_date >= start_date");
                    table.CheckConstraint("leave_application_days_check", "days > 0");
                    table.ForeignKey(
                        name: "leave_application_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "leave_application_leave_type_id_fkey",
                        column: x => x.leave_type_id,
                        principalSchema: "hrms",
                        principalTable: "leave_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "leave_entitlement",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    entitled_days = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("leave_entitlement_pkey", x => x.id);
                    table.CheckConstraint("leave_entitlement_entitled_days_check", "entitled_days >= 0");
                    table.CheckConstraint("leave_entitlement_year_check", "year BETWEEN 2000 AND 2100");
                    table.ForeignKey(
                        name: "leave_entitlement_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "leave_entitlement_leave_type_id_fkey",
                        column: x => x.leave_type_id,
                        principalSchema: "hrms",
                        principalTable: "leave_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "holiday_calendar",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    holiday_date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    holiday_type = table.Column<HolidayType>(type: "hrms.holiday_type_enum", nullable: false, defaultValue: HolidayType.Public),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("holiday_calendar_pkey", x => x.id);
                    table.ForeignKey(
                        name: "holiday_calendar_location_id_fkey",
                        column: x => x.location_id,
                        principalSchema: "hrms",
                        principalTable: "location",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pay_scale_version",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_basic_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    max_basic_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    increment_rule = table.Column<string>(type: "text", nullable: true),
                    version_label = table.Column<string>(type: "varchar", nullable: true),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pay_scale_version_pkey", x => x.id);
                    table.CheckConstraint("ck_psv_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_psv_pay", "max_basic_pay >= min_basic_pay");
                    table.CheckConstraint("pay_scale_version_min_basic_pay_check", "min_basic_pay >= 0");
                    table.ForeignKey(
                        name: "pay_scale_version_grade_id_fkey",
                        column: x => x.grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_run",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_type = table.Column<PayrollRunType>(type: "hrms.payroll_run_type_enum", nullable: false, defaultValue: PayrollRunType.Regular),
                    run_label = table.Column<string>(type: "varchar", nullable: true),
                    status = table.Column<PayrollRunStatus>(type: "hrms.payroll_run_status_enum", nullable: false, defaultValue: PayrollRunStatus.Draft),
                    reverses_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prepared_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_run_pkey", x => x.id);
                    table.CheckConstraint("ck_pr_approved", "status NOT IN ('approved','finalized','paid') OR approved_by IS NOT NULL");
                    table.CheckConstraint("ck_pr_reverses", "reverses_run_id IS NULL OR reverses_run_id <> id");
                    table.ForeignKey(
                        name: "payroll_run_payroll_period_id_fkey",
                        column: x => x.payroll_period_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_period",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_run_reverses_run_id_fkey",
                        column: x => x.reverses_run_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_run",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "performance_review",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performance_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<ReviewStatus>(type: "hrms.review_status_enum", nullable: false, defaultValue: ReviewStatus.Draft),
                    final_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    performance_grade = table.Column<string>(type: "varchar", nullable: true),
                    promotion_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    training_recommended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_review_pkey", x => x.id);
                    table.CheckConstraint("ck_pr_not_self", "employee_id <> evaluator_id");
                    table.CheckConstraint("performance_review_final_score_check", "final_score IS NULL OR final_score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_review_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "performance_review_evaluator_id_fkey",
                        column: x => x.evaluator_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "performance_review_performance_period_id_fkey",
                        column: x => x.performance_period_id,
                        principalSchema: "hrms",
                        principalTable: "performance_period",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_version",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    head_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organization_unit_version_pkey", x => x.id);
                    table.CheckConstraint("ck_ouv_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_ouv_no_self", "parent_unit_id IS NULL OR parent_unit_id <> org_unit_id");
                    table.ForeignKey(
                        name: "fk_ouv_head_post",
                        column: x => x.head_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_unit_version_location_id_fkey",
                        column: x => x.location_id,
                        principalSchema: "hrms",
                        principalTable: "location",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_unit_version_org_unit_id_fkey",
                        column: x => x.org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_unit_version_parent_unit_id_fkey",
                        column: x => x.parent_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "organization_unit_version_unit_type_id_fkey",
                        column: x => x.unit_type_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_version",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    designation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporting_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    employment_type = table.Column<EmploymentType>(type: "hrms.employment_type_enum", nullable: false, defaultValue: EmploymentType.Regular),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sanctioned_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    lifecycle_status = table.Column<PostLifecycle>(type: "hrms.post_lifecycle_enum", nullable: false, defaultValue: PostLifecycle.Sanctioned),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("post_version_pkey", x => x.id);
                    table.CheckConstraint("ck_pv_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_pv_self", "reporting_post_id IS NULL OR reporting_post_id <> post_id");
                    table.CheckConstraint("post_version_sanctioned_count_check", "sanctioned_count >= 1");
                    table.ForeignKey(
                        name: "post_version_designation_id_fkey",
                        column: x => x.designation_id,
                        principalSchema: "hrms",
                        principalTable: "designation",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "post_version_grade_id_fkey",
                        column: x => x.grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "post_version_location_id_fkey",
                        column: x => x.location_id,
                        principalSchema: "hrms",
                        principalTable: "location",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "post_version_org_unit_id_fkey",
                        column: x => x.org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "post_version_post_id_fkey",
                        column: x => x.post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "post_version_reporting_post_id_fkey",
                        column: x => x.reporting_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_salary_component",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    override_fixed_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    override_percentage = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_salary_component_pkey", x => x.id);
                    table.CheckConstraint("ck_esc_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_esc_one", "num_nonnulls(override_fixed_amount, override_percentage) = 1");
                    table.CheckConstraint("employee_salary_component_override_fixed_amount_check", "override_fixed_amount IS NULL OR override_fixed_amount >= 0");
                    table.CheckConstraint("employee_salary_component_override_percentage_check", "override_percentage IS NULL OR override_percentage BETWEEN 0 AND 1000");
                    table.ForeignKey(
                        name: "employee_salary_component_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_salary_component_post_id_fkey",
                        column: x => x.post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_salary_component_salary_component_id_fkey",
                        column: x => x.salary_component_id,
                        principalSchema: "hrms",
                        principalTable: "salary_component",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "loan_type",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "varchar", nullable: false),
                    salary_component_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_gpf_advance = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_interest_rate = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false, defaultValueSql: "0"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("loan_type_pkey", x => x.id);
                    table.CheckConstraint("loan_type_default_interest_rate_check", "default_interest_rate >= 0");
                    table.ForeignKey(
                        name: "loan_type_salary_component_id_fkey",
                        column: x => x.salary_component_id,
                        principalSchema: "hrms",
                        principalTable: "salary_component",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "salary_component_rule",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    salary_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_version = table.Column<string>(type: "varchar", nullable: false),
                    calculation_method = table.Column<CalculationMethod>(type: "hrms.calculation_method_enum", nullable: false),
                    fixed_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    percentage = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    calculation_base = table.Column<string>(type: "varchar", nullable: true),
                    formula_expression = table.Column<string>(type: "text", nullable: true),
                    min_bps = table.Column<int>(type: "integer", nullable: true),
                    max_bps = table.Column<int>(type: "integer", nullable: true),
                    applicable_designation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applicable_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applicable_employment_type = table.Column<EmploymentType>(type: "hrms.employment_type_enum", nullable: true),
                    min_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    max_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("salary_component_rule_pkey", x => x.id);
                    table.CheckConstraint("ck_scr_amount", "min_amount IS NULL OR max_amount IS NULL OR min_amount <= max_amount");
                    table.CheckConstraint("ck_scr_bps", "min_bps IS NULL OR max_bps IS NULL OR min_bps <= max_bps");
                    table.CheckConstraint("ck_scr_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("ck_scr_method", "(calculation_method = 'fixed' AND fixed_amount IS NOT NULL) OR (calculation_method = 'percentage' AND percentage IS NOT NULL AND calculation_base IS NOT NULL) OR (calculation_method = 'formula' AND formula_expression IS NOT NULL) OR (calculation_method = 'tiered')");
                    table.CheckConstraint("salary_component_rule_fixed_amount_check", "fixed_amount IS NULL OR fixed_amount >= 0");
                    table.CheckConstraint("salary_component_rule_max_bps_check", "max_bps IS NULL OR max_bps BETWEEN 1 AND 22");
                    table.CheckConstraint("salary_component_rule_min_bps_check", "min_bps IS NULL OR min_bps BETWEEN 1 AND 22");
                    table.CheckConstraint("salary_component_rule_percentage_check", "percentage IS NULL OR percentage BETWEEN 0 AND 1000");
                    table.ForeignKey(
                        name: "salary_component_rule_applicable_designation_id_fkey",
                        column: x => x.applicable_designation_id,
                        principalSchema: "hrms",
                        principalTable: "designation",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "salary_component_rule_applicable_org_unit_id_fkey",
                        column: x => x.applicable_org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "salary_component_rule_salary_component_id_fkey",
                        column: x => x.salary_component_id,
                        principalSchema: "hrms",
                        principalTable: "salary_component",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_tax_exemption",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exemption_type = table.Column<string>(type: "varchar", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_tax_exemption_pkey", x => x.id);
                    table.CheckConstraint("employee_tax_exemption_amount_check", "amount >= 0");
                    table.ForeignKey(
                        name: "employee_tax_exemption_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_tax_exemption_tax_year_id_fkey",
                        column: x => x.tax_year_id,
                        principalSchema: "hrms",
                        principalTable: "tax_year",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tax_slab",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slab_order = table.Column<int>(type: "integer", nullable: false),
                    min_income = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    max_income = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    fixed_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    rate_percentage = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false, defaultValueSql: "0")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tax_slab_pkey", x => x.id);
                    table.CheckConstraint("ck_slab_range", "max_income IS NULL OR max_income > min_income");
                    table.CheckConstraint("tax_slab_fixed_amount_check", "fixed_amount >= 0");
                    table.CheckConstraint("tax_slab_min_income_check", "min_income >= 0");
                    table.CheckConstraint("tax_slab_rate_percentage_check", "rate_percentage BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "tax_slab_tax_year_id_fkey",
                        column: x => x.tax_year_id,
                        principalSchema: "hrms",
                        principalTable: "tax_year",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "attendance_record",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    work_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    working_hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    overtime_hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    late_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    early_departure_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<AttendanceStatus>(type: "hrms.attendance_status_enum", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("attendance_record_pkey", x => x.id);
                    table.CheckConstraint("attendance_record_early_departure_minutes_check", "early_departure_minutes >= 0");
                    table.CheckConstraint("attendance_record_late_minutes_check", "late_minutes >= 0");
                    table.CheckConstraint("attendance_record_overtime_hours_check", "overtime_hours IS NULL OR overtime_hours BETWEEN 0 AND 24");
                    table.CheckConstraint("attendance_record_working_hours_check", "working_hours IS NULL OR working_hours BETWEEN 0 AND 24");
                    table.ForeignKey(
                        name: "attendance_record_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "attendance_record_work_shift_id_fkey",
                        column: x => x.work_shift_id,
                        principalSchema: "hrms",
                        principalTable: "work_shift",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_shift",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_shift_pkey", x => x.id);
                    table.CheckConstraint("ck_es_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.ForeignKey(
                        name: "employee_shift_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_shift_work_shift_id_fkey",
                        column: x => x.work_shift_id,
                        principalSchema: "hrms",
                        principalTable: "work_shift",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_request",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "varchar", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    requested_data = table.Column<string>(type: "jsonb", nullable: true),
                    supporting_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<EmployeeRequestStatus>(type: "hrms.employee_request_status_enum", nullable: false, defaultValue: EmployeeRequestStatus.Pending),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_request_pkey", x => x.id);
                    table.CheckConstraint("ck_er_review", "status NOT IN ('approved','rejected') OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "employee_request_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_request_request_type_id_fkey",
                        column: x => x.request_type_id,
                        principalSchema: "hrms",
                        principalTable: "employee_request_type",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_request_supporting_document_id_fkey",
                        column: x => x.supporting_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_service_history",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    old_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recruitment_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_reference_org = table.Column<string>(type: "varchar", nullable: true),
                    order_number = table.Column<string>(type: "varchar", nullable: true),
                    supporting_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_service_history_pkey", x => x.id);
                    table.ForeignKey(
                        name: "employee_service_history_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_event_type_id_fkey",
                        column: x => x.event_type_id,
                        principalSchema: "hrms",
                        principalTable: "service_event_type",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_new_grade_id_fkey",
                        column: x => x.new_grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_new_org_unit_id_fkey",
                        column: x => x.new_org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_new_post_id_fkey",
                        column: x => x.new_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_old_grade_id_fkey",
                        column: x => x.old_grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_old_org_unit_id_fkey",
                        column: x => x.old_org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_old_post_id_fkey",
                        column: x => x.old_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_recruitment_method_id_fkey",
                        column: x => x.recruitment_method_id,
                        principalSchema: "hrms",
                        principalTable: "recruitment_method",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_service_history_supporting_document_id_fkey",
                        column: x => x.supporting_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_task_update",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_percentage = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_task_update_pkey", x => x.id);
                    table.CheckConstraint("employee_task_update_progress_percentage_check", "progress_percentage BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "employee_task_update_task_id_fkey",
                        column: x => x.task_id,
                        principalSchema: "hrms",
                        principalTable: "employee_task",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "leave_ledger",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    txn_type = table.Column<LeaveTransactionType>(type: "hrms.leave_txn_type_enum", nullable: false),
                    days = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    txn_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    leave_application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("leave_ledger_pkey", x => x.id);
                    table.CheckConstraint("ck_ll_app", "txn_type NOT IN ('usage','usage_reversal') OR leave_application_id IS NOT NULL");
                    table.CheckConstraint("ck_ll_sign", "(txn_type IN ('opening','accrual','carry_forward','usage_reversal') AND days > 0) OR (txn_type IN ('usage','lapse','encashment') AND days < 0) OR txn_type = 'adjustment'");
                    table.CheckConstraint("leave_ledger_days_check", "days <> 0");
                    table.ForeignKey(
                        name: "leave_ledger_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "leave_ledger_leave_application_id_fkey",
                        column: x => x.leave_application_id,
                        principalSchema: "hrms",
                        principalTable: "leave_application",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "leave_ledger_leave_type_id_fkey",
                        column: x => x.leave_type_id,
                        principalSchema: "hrms",
                        principalTable: "leave_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pay_scale_stage",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    pay_scale_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_number = table.Column<int>(type: "integer", nullable: false),
                    basic_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pay_scale_stage_pkey", x => x.id);
                    table.CheckConstraint("pay_scale_stage_basic_pay_check", "basic_pay >= 0");
                    table.CheckConstraint("pay_scale_stage_stage_number_check", "stage_number >= 0");
                    table.ForeignKey(
                        name: "pay_scale_stage_pay_scale_version_id_fkey",
                        column: x => x.pay_scale_version_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_version",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_transaction",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    days_payable = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    gross_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    total_deductions = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    net_payable = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    status = table.Column<PayrollTransactionStatus>(type: "hrms.payroll_txn_status_enum", nullable: false, defaultValue: PayrollTransactionStatus.Calculated),
                    remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_transaction_pkey", x => x.id);
                    table.CheckConstraint("ck_pt_net", "net_payable = gross_pay - total_deductions");
                    table.CheckConstraint("payroll_transaction_days_payable_check", "days_payable IS NULL OR days_payable BETWEEN 0 AND 31");
                    table.CheckConstraint("payroll_transaction_gross_pay_check", "gross_pay >= 0");
                    table.CheckConstraint("payroll_transaction_total_deductions_check", "total_deductions >= 0");
                    table.ForeignKey(
                        name: "payroll_transaction_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_transaction_payroll_run_id_fkey",
                        column: x => x.payroll_run_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_run",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "performance_competency",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_name = table.Column<string>(type: "varchar", nullable: false),
                    rating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_competency_pkey", x => x.id);
                    table.CheckConstraint("performance_competency_rating_check", "rating IS NULL OR rating BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "performance_competency_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_goal",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValueSql: "0"),
                    target = table.Column<string>(type: "text", nullable: true),
                    achievement = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_goal_pkey", x => x.id);
                    table.CheckConstraint("performance_goal_score_check", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.CheckConstraint("performance_goal_weight_check", "weight BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_goal_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "performance_kpi",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_name = table.Column<string>(type: "varchar", nullable: false),
                    target_value = table.Column<string>(type: "varchar", nullable: true),
                    achieved_value = table.Column<string>(type: "varchar", nullable: true),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("performance_kpi_pkey", x => x.id);
                    table.CheckConstraint("performance_kpi_score_check", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "performance_kpi_performance_review_id_fkey",
                        column: x => x.performance_review_id,
                        principalSchema: "hrms",
                        principalTable: "performance_review",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_loan",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    interest_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    installments_count = table.Column<int>(type: "integer", nullable: false),
                    monthly_installment = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    remaining_balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    deduction_priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    status = table.Column<LoanStatus>(type: "hrms.loan_status_enum", nullable: false, defaultValue: LoanStatus.Active),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_loan_pkey", x => x.id);
                    table.CheckConstraint("ck_el_dates", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("employee_loan_installments_count_check", "installments_count > 0");
                    table.CheckConstraint("employee_loan_interest_amount_check", "interest_amount >= 0");
                    table.CheckConstraint("employee_loan_monthly_installment_check", "monthly_installment > 0");
                    table.CheckConstraint("employee_loan_principal_amount_check", "principal_amount > 0");
                    table.CheckConstraint("employee_loan_remaining_balance_check", "remaining_balance >= 0");
                    table.ForeignKey(
                        name: "employee_loan_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_loan_loan_type_id_fkey",
                        column: x => x.loan_type_id,
                        principalSchema: "hrms",
                        principalTable: "loan_type",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "hr_action_request",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action_type = table.Column<HrActionType>(type: "hrms.hr_action_type_enum", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_grade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    order_number = table.Column<string>(type: "varchar", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supporting_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<HrActionStatus>(type: "hrms.hr_action_status_enum", nullable: false, defaultValue: HrActionStatus.Draft),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resulting_service_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("hr_action_request_pkey", x => x.id);
                    table.CheckConstraint("ck_har_applied", "status <> 'applied' OR resulting_service_history_id IS NOT NULL");
                    table.ForeignKey(
                        name: "hr_action_request_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_new_grade_id_fkey",
                        column: x => x.new_grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_new_org_unit_id_fkey",
                        column: x => x.new_org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_new_post_id_fkey",
                        column: x => x.new_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_old_grade_id_fkey",
                        column: x => x.old_grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_old_org_unit_id_fkey",
                        column: x => x.old_org_unit_id,
                        principalSchema: "hrms",
                        principalTable: "organization_unit",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_old_post_id_fkey",
                        column: x => x.old_post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_resulting_service_history_id_fkey",
                        column: x => x.resulting_service_history_id,
                        principalSchema: "hrms",
                        principalTable: "employee_service_history",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "hr_action_request_supporting_document_id_fkey",
                        column: x => x.supporting_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "position_assignment",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_type = table.Column<AssignmentType>(type: "hrms.assignment_type_enum", nullable: false, defaultValue: AssignmentType.Regular),
                    service_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_number = table.Column<string>(type: "varchar", nullable: true),
                    order_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<RecordStatus>(type: "hrms.record_status_enum", nullable: false, defaultValue: RecordStatus.Active),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("position_assignment_pkey", x => x.id);
                    table.CheckConstraint("ck_pa_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.ForeignKey(
                        name: "position_assignment_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "position_assignment_order_document_id_fkey",
                        column: x => x.order_document_id,
                        principalSchema: "hrms",
                        principalTable: "employee_document",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "position_assignment_post_id_fkey",
                        column: x => x.post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "position_assignment_service_history_id_fkey",
                        column: x => x.service_history_id,
                        principalSchema: "hrms",
                        principalTable: "employee_service_history",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_pay_record",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_scale_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basic_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    last_increment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_increment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "varchar", nullable: true),
                    order_number = table.Column<string>(type: "varchar", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_pay_record_pkey", x => x.id);
                    table.CheckConstraint("ck_epr_dates", "effective_to IS NULL OR effective_to >= effective_from");
                    table.CheckConstraint("employee_pay_record_basic_pay_check", "basic_pay >= 0");
                    table.ForeignKey(
                        name: "employee_pay_record_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_pay_record_pay_scale_stage_id_fkey",
                        column: x => x.pay_scale_stage_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_stage",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_separation",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    separation_type = table.Column<SeparationType>(type: "hrms.separation_type_enum", nullable: false),
                    separation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_history_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement_payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_number = table.Column<string>(type: "varchar", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    notice_period_days = table.Column<int>(type: "integer", nullable: true),
                    final_settlement_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    outstanding_loan_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    leave_encashment_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    pension_reference = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_separation_pkey", x => x.id);
                    table.CheckConstraint("employee_separation_leave_encashment_amount_check", "leave_encashment_amount IS NULL OR leave_encashment_amount >= 0");
                    table.CheckConstraint("employee_separation_notice_period_days_check", "notice_period_days IS NULL OR notice_period_days >= 0");
                    table.CheckConstraint("employee_separation_outstanding_loan_amount_check", "outstanding_loan_amount IS NULL OR outstanding_loan_amount >= 0");
                    table.ForeignKey(
                        name: "employee_separation_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_separation_service_history_id_fkey",
                        column: x => x.service_history_id,
                        principalSchema: "hrms",
                        principalTable: "employee_service_history",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_sep_settlement",
                        column: x => x.settlement_payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "employee_tax_ledger",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxable_income = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("employee_tax_ledger_pkey", x => x.id);
                    table.CheckConstraint("employee_tax_ledger_tax_withheld_check", "tax_withheld >= 0");
                    table.CheckConstraint("employee_tax_ledger_taxable_income_check", "taxable_income >= 0");
                    table.ForeignKey(
                        name: "employee_tax_ledger_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_tax_ledger_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "employee_tax_ledger_tax_year_id_fkey",
                        column: x => x.tax_year_id,
                        principalSchema: "hrms",
                        principalTable: "tax_year",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_adjustment",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<AdjustmentType>(type: "hrms.adjustment_type_enum", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_adjustment_pkey", x => x.id);
                    table.CheckConstraint("payroll_adjustment_amount_check", "amount <> 0");
                    table.ForeignKey(
                        name: "payroll_adjustment_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_payment",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name_snapshot = table.Column<string>(type: "varchar", nullable: true),
                    branch_name_snapshot = table.Column<string>(type: "varchar", nullable: true),
                    account_number_snapshot = table.Column<string>(type: "varchar", nullable: true),
                    iban_snapshot = table.Column<string>(type: "varchar", nullable: true),
                    payment_method = table.Column<PaymentMethod>(type: "hrms.payment_method_enum", nullable: false, defaultValue: PaymentMethod.BankTransfer),
                    payment_status = table.Column<PaymentStatus>(type: "hrms.payment_status_enum", nullable: false, defaultValue: PaymentStatus.Pending),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    payment_reference = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_payment_pkey", x => x.id);
                    table.CheckConstraint("ck_pp_processed", "payment_status <> 'processed' OR (payment_date IS NOT NULL AND payment_reference IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_pp_bank_owner",
                        columns: x => new { x.bank_account_id, x.employee_id },
                        principalSchema: "hrms",
                        principalTable: "employee_bank_account",
                        principalColumns: new[] { "id", "employee_id" });
                    table.ForeignKey(
                        name: "payroll_payment_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "hrms",
                        principalTable: "employee",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_payment_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_transaction_segment",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_scale_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    basic_pay = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_transaction_segment_pkey", x => x.id);
                    table.CheckConstraint("ck_pts_dates", "period_to >= period_from");
                    table.CheckConstraint("payroll_transaction_segment_basic_pay_check", "basic_pay >= 0");
                    table.CheckConstraint("payroll_transaction_segment_days_check", "days > 0 AND days <= 31");
                    table.ForeignKey(
                        name: "payroll_transaction_segment_grade_id_fkey",
                        column: x => x.grade_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_grade",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_transaction_segment_pay_scale_stage_id_fkey",
                        column: x => x.pay_scale_stage_id,
                        principalSchema: "hrms",
                        principalTable: "pay_scale_stage",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_transaction_segment_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_transaction_segment_post_id_fkey",
                        column: x => x.post_id,
                        principalSchema: "hrms",
                        principalTable: "post",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payslip",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    file_reference = table.Column<string>(type: "varchar", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payslip_pkey", x => x.id);
                    table.ForeignKey(
                        name: "payslip_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "gp_fund_transaction",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    gp_fund_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    txn_type = table.Column<GpFundTransactionType>(type: "hrms.gpf_txn_type_enum", nullable: false),
                    txn_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    employee_loan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    remarks = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("gp_fund_transaction_pkey", x => x.id);
                    table.CheckConstraint("ck_gpt_sign", "(txn_type IN ('opening','subscription','interest','advance_recovery') AND amount > 0) OR (txn_type IN ('advance','withdrawal','final_payment') AND amount < 0) OR txn_type = 'adjustment'");
                    table.CheckConstraint("gp_fund_transaction_amount_check", "amount <> 0");
                    table.ForeignKey(
                        name: "fk_gpt_payroll",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "gp_fund_transaction_employee_loan_id_fkey",
                        column: x => x.employee_loan_id,
                        principalSchema: "hrms",
                        principalTable: "employee_loan",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "gp_fund_transaction_gp_fund_account_id_fkey",
                        column: x => x.gp_fund_account_id,
                        principalSchema: "hrms",
                        principalTable: "gp_fund_account",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "loan_installment_schedule",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    employee_loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValueSql: "0"),
                    status = table.Column<InstallmentStatus>(type: "hrms.installment_status_enum", nullable: false, defaultValue: InstallmentStatus.Pending)
                },
                constraints: table =>
                {
                    table.PrimaryKey("loan_installment_schedule_pkey", x => x.id);
                    table.UniqueConstraint("uq_lis_id_loan", x => new { x.id, x.employee_loan_id });
                    table.CheckConstraint("ck_lis_paid", "paid_amount <= amount");
                    table.CheckConstraint("loan_installment_schedule_amount_check", "amount > 0");
                    table.CheckConstraint("loan_installment_schedule_installment_number_check", "installment_number > 0");
                    table.CheckConstraint("loan_installment_schedule_paid_amount_check", "paid_amount >= 0");
                    table.ForeignKey(
                        name: "loan_installment_schedule_employee_loan_id_fkey",
                        column: x => x.employee_loan_id,
                        principalSchema: "hrms",
                        principalTable: "employee_loan",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_loan_deduction",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_loan_deduction_pkey", x => x.id);
                    table.CheckConstraint("payroll_loan_deduction_installment_amount_check", "installment_amount > 0");
                    table.ForeignKey(
                        name: "fk_pld_installment",
                        columns: x => new { x.installment_id, x.employee_loan_id },
                        principalSchema: "hrms",
                        principalTable: "loan_installment_schedule",
                        principalColumns: new[] { "id", "employee_loan_id" });
                    table.ForeignKey(
                        name: "payroll_loan_deduction_employee_loan_id_fkey",
                        column: x => x.employee_loan_id,
                        principalSchema: "hrms",
                        principalTable: "employee_loan",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_loan_deduction_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payroll_component_detail",
                schema: "hrms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    payroll_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salary_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_component_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<ComponentSource>(type: "hrms.component_source_enum", nullable: false, defaultValue: ComponentSource.Rule),
                    payroll_loan_deduction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    component_type = table.Column<ComponentType>(type: "hrms.component_type_enum", nullable: false),
                    calculation_base = table.Column<string>(type: "varchar", nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: true),
                    formula_reference = table.Column<string>(type: "text", nullable: true),
                    calculated_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    notification_ref = table.Column<string>(type: "varchar", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("payroll_component_detail_pkey", x => x.id);
                    table.CheckConstraint("ck_pcd_loan", "(source = 'loan') = (payroll_loan_deduction_id IS NOT NULL)");
                    table.CheckConstraint("ck_pcd_loan_type", "source <> 'loan' OR component_type = 'deduction'");
                    table.CheckConstraint("payroll_component_detail_calculated_amount_check", "calculated_amount >= 0");
                    table.ForeignKey(
                        name: "payroll_component_detail_payroll_loan_deduction_id_fkey",
                        column: x => x.payroll_loan_deduction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_loan_deduction",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_component_detail_payroll_transaction_id_fkey",
                        column: x => x.payroll_transaction_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_component_detail_salary_component_id_fkey",
                        column: x => x.salary_component_id,
                        principalSchema: "hrms",
                        principalTable: "salary_component",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_component_detail_salary_component_rule_id_fkey",
                        column: x => x.salary_component_rule_id,
                        principalSchema: "hrms",
                        principalTable: "salary_component_rule",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "payroll_component_detail_segment_id_fkey",
                        column: x => x.segment_id,
                        principalSchema: "hrms",
                        principalTable: "payroll_transaction_segment",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "attendance_record_employee_id_attendance_date_key",
                schema: "hrms",
                table: "attendance_record",
                columns: new[] { "employee_id", "attendance_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_att_date",
                schema: "hrms",
                table: "attendance_record",
                column: "attendance_date");

            migrationBuilder.CreateIndex(
                name: "idx_attendance_record_work_shift_id",
                schema: "hrms",
                table: "attendance_record",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "designation_code_key",
                schema: "hrms",
                table: "designation",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "designation_title_key",
                schema: "hrms",
                table: "designation",
                column: "title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "document_type_name_key",
                schema: "hrms",
                table: "document_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "employee_cnic_key",
                schema: "hrms",
                table: "employee",
                column: "cnic",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "employee_employee_number_key",
                schema: "hrms",
                table: "employee",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "employee_user_id_key",
                schema: "hrms",
                table: "employee",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_status",
                schema: "hrms",
                table: "employee",
                column: "employment_status");

            migrationBuilder.CreateIndex(
                name: "idx_employee_type",
                schema: "hrms",
                table: "employee",
                column: "employment_type");

            migrationBuilder.CreateIndex(
                name: "uq_employee_address_type",
                schema: "hrms",
                table: "employee_address",
                columns: new[] { "employee_id", "address_type" },
                unique: true,
                filter: "address_type IN ('permanent', 'current')");

            migrationBuilder.CreateIndex(
                name: "idx_ed_employee",
                schema: "hrms",
                table: "employee_document",
                columns: new[] { "employee_id", "document_type_id" });

            migrationBuilder.CreateIndex(
                name: "idx_ed_expiry",
                schema: "hrms",
                table: "employee_document",
                column: "expiry_date",
                filter: "expiry_date IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_employee_document_document_type_id",
                schema: "hrms",
                table: "employee_document",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_edu_employee_level",
                schema: "hrms",
                table: "employee_education",
                columns: new[] { "employee_id", "qualification_level" });

            migrationBuilder.CreateIndex(
                name: "idx_employee_emergency_contact_employee_id",
                schema: "hrms",
                table: "employee_emergency_contact",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_family_member_employee_id",
                schema: "hrms",
                table: "employee_family_member",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_el_employee_status",
                schema: "hrms",
                table: "employee_loan",
                columns: new[] { "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_employee_loan_loan_type_id",
                schema: "hrms",
                table: "employee_loan",
                column: "loan_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_pay_record_pay_scale_stage_id",
                schema: "hrms",
                table: "employee_pay_record",
                column: "pay_scale_stage_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_request_supporting_document_id",
                schema: "hrms",
                table: "employee_request",
                column: "supporting_document_id");

            migrationBuilder.CreateIndex(
                name: "idx_er_employee",
                schema: "hrms",
                table: "employee_request",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_er_status",
                schema: "hrms",
                table: "employee_request",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_er_type",
                schema: "hrms",
                table: "employee_request",
                column: "request_type_id");

            migrationBuilder.CreateIndex(
                name: "employee_request_type_code_key",
                schema: "hrms",
                table: "employee_request_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "employee_request_type_name_key",
                schema: "hrms",
                table: "employee_request_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_salary_component_post_id",
                schema: "hrms",
                table: "employee_salary_component",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_salary_component_salary_component_id",
                schema: "hrms",
                table: "employee_salary_component",
                column: "salary_component_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_separation_service_history_id",
                schema: "hrms",
                table: "employee_separation",
                column: "service_history_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_separation_settlement_payroll_transaction_id",
                schema: "hrms",
                table: "employee_separation",
                column: "settlement_payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "uq_separation_employee",
                schema: "hrms",
                table: "employee_separation",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_new_grade_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "new_grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_new_org_unit_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "new_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_new_post_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "new_post_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_old_grade_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "old_grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_old_org_unit_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "old_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_old_post_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "old_post_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_recruitment_method_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "recruitment_method_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_service_history_supporting_document_id",
                schema: "hrms",
                table: "employee_service_history",
                column: "supporting_document_id");

            migrationBuilder.CreateIndex(
                name: "idx_esh_employee_date",
                schema: "hrms",
                table: "employee_service_history",
                columns: new[] { "employee_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "idx_esh_event",
                schema: "hrms",
                table: "employee_service_history",
                column: "event_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_employee_shift_work_shift_id",
                schema: "hrms",
                table: "employee_shift",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "idx_et_due",
                schema: "hrms",
                table: "employee_task",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "idx_et_employee",
                schema: "hrms",
                table: "employee_task",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_et_status",
                schema: "hrms",
                table: "employee_task",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_etu_task",
                schema: "hrms",
                table: "employee_task_update",
                columns: new[] { "task_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "employee_tax_exemption_employee_id_tax_year_id_exemption_ty_key",
                schema: "hrms",
                table: "employee_tax_exemption",
                columns: new[] { "employee_id", "tax_year_id", "exemption_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_tax_exemption_tax_year_id",
                schema: "hrms",
                table: "employee_tax_exemption",
                column: "tax_year_id");

            migrationBuilder.CreateIndex(
                name: "employee_tax_ledger_payroll_transaction_id_key",
                schema: "hrms",
                table: "employee_tax_ledger",
                column: "payroll_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_employee_tax_ledger_tax_year_id",
                schema: "hrms",
                table: "employee_tax_ledger",
                column: "tax_year_id");

            migrationBuilder.CreateIndex(
                name: "idx_etl_emp_year",
                schema: "hrms",
                table: "employee_tax_ledger",
                columns: new[] { "employee_id", "tax_year_id" });

            migrationBuilder.CreateIndex(
                name: "gp_fund_account_account_number_key",
                schema: "hrms",
                table: "gp_fund_account",
                column: "account_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "gp_fund_account_employee_id_key",
                schema: "hrms",
                table: "gp_fund_account",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_gp_fund_transaction_employee_loan_id",
                schema: "hrms",
                table: "gp_fund_transaction",
                column: "employee_loan_id");

            migrationBuilder.CreateIndex(
                name: "idx_gp_fund_transaction_payroll_transaction_id",
                schema: "hrms",
                table: "gp_fund_transaction",
                column: "payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "idx_gpt_account_date",
                schema: "hrms",
                table: "gp_fund_transaction",
                columns: new[] { "gp_fund_account_id", "txn_date" });

            migrationBuilder.CreateIndex(
                name: "holiday_calendar_holiday_date_name_location_id_key",
                schema: "hrms",
                table: "holiday_calendar",
                columns: new[] { "holiday_date", "name", "location_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "idx_holiday_calendar_location_id",
                schema: "hrms",
                table: "holiday_calendar",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "idx_holiday_date",
                schema: "hrms",
                table: "holiday_calendar",
                column: "holiday_date");

            migrationBuilder.CreateIndex(
                name: "idx_har_employee",
                schema: "hrms",
                table: "hr_action_request",
                columns: new[] { "employee_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "idx_har_status",
                schema: "hrms",
                table: "hr_action_request",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_new_grade_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "new_grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_new_org_unit_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "new_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_new_post_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "new_post_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_old_grade_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "old_grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_old_org_unit_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "old_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_old_post_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "old_post_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_resulting_service_history_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "resulting_service_history_id");

            migrationBuilder.CreateIndex(
                name: "idx_hr_action_request_supporting_document_id",
                schema: "hrms",
                table: "hr_action_request",
                column: "supporting_document_id");

            migrationBuilder.CreateIndex(
                name: "idx_la_employee_start",
                schema: "hrms",
                table: "leave_application",
                columns: new[] { "employee_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "idx_la_status",
                schema: "hrms",
                table: "leave_application",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_leave_application_leave_type_id",
                schema: "hrms",
                table: "leave_application",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_leave_entitlement_leave_type_id",
                schema: "hrms",
                table: "leave_entitlement",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "leave_entitlement_employee_id_leave_type_id_year_key",
                schema: "hrms",
                table: "leave_entitlement",
                columns: new[] { "employee_id", "leave_type_id", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_leave_ledger_leave_application_id",
                schema: "hrms",
                table: "leave_ledger",
                column: "leave_application_id");

            migrationBuilder.CreateIndex(
                name: "idx_leave_ledger_leave_type_id",
                schema: "hrms",
                table: "leave_ledger",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_ll_emp_type_year",
                schema: "hrms",
                table: "leave_ledger",
                columns: new[] { "employee_id", "leave_type_id", "year" });

            migrationBuilder.CreateIndex(
                name: "leave_type_name_key",
                schema: "hrms",
                table: "leave_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_lis_due",
                schema: "hrms",
                table: "loan_installment_schedule",
                column: "due_date",
                filter: "status IN ('pending', 'partial')");

            migrationBuilder.CreateIndex(
                name: "loan_installment_schedule_employee_loan_id_installment_numb_key",
                schema: "hrms",
                table: "loan_installment_schedule",
                columns: new[] { "employee_loan_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_loan_type_salary_component_id",
                schema: "hrms",
                table: "loan_type",
                column: "salary_component_id");

            migrationBuilder.CreateIndex(
                name: "loan_type_name_key",
                schema: "hrms",
                table: "loan_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "organization_unit_code_key",
                schema: "hrms",
                table: "organization_unit",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "organization_unit_type_code_key",
                schema: "hrms",
                table: "organization_unit_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "organization_unit_type_name_key",
                schema: "hrms",
                table: "organization_unit_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_organization_unit_version_head_post_id",
                schema: "hrms",
                table: "organization_unit_version",
                column: "head_post_id");

            migrationBuilder.CreateIndex(
                name: "idx_organization_unit_version_location_id",
                schema: "hrms",
                table: "organization_unit_version",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "idx_organization_unit_version_unit_type_id",
                schema: "hrms",
                table: "organization_unit_version",
                column: "unit_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_ouv_parent",
                schema: "hrms",
                table: "organization_unit_version",
                column: "parent_unit_id");

            migrationBuilder.CreateIndex(
                name: "pay_scale_grade_bps_number_key",
                schema: "hrms",
                table: "pay_scale_grade",
                column: "bps_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "pay_scale_stage_pay_scale_version_id_stage_number_key",
                schema: "hrms",
                table: "pay_scale_stage",
                columns: new[] { "pay_scale_version_id", "stage_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payroll_adjustment_payroll_transaction_id",
                schema: "hrms",
                table: "payroll_adjustment",
                column: "payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_component_detail_salary_component_id",
                schema: "hrms",
                table: "payroll_component_detail",
                column: "salary_component_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_component_detail_salary_component_rule_id",
                schema: "hrms",
                table: "payroll_component_detail",
                column: "salary_component_rule_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_component_detail_segment_id",
                schema: "hrms",
                table: "payroll_component_detail",
                column: "segment_id");

            migrationBuilder.CreateIndex(
                name: "idx_pcd_txn",
                schema: "hrms",
                table: "payroll_component_detail",
                column: "payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "payroll_component_detail_payroll_loan_deduction_id_key",
                schema: "hrms",
                table: "payroll_component_detail",
                column: "payroll_loan_deduction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payroll_loan_deduction_employee_loan_id",
                schema: "hrms",
                table: "payroll_loan_deduction",
                column: "employee_loan_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_loan_deduction_installment_id_employee_loan_id",
                schema: "hrms",
                table: "payroll_loan_deduction",
                columns: new[] { "installment_id", "employee_loan_id" });

            migrationBuilder.CreateIndex(
                name: "idx_payroll_loan_deduction_payroll_transaction_id",
                schema: "hrms",
                table: "payroll_loan_deduction",
                column: "payroll_transaction_id");

            migrationBuilder.CreateIndex(
                name: "payroll_loan_deduction_installment_id_key",
                schema: "hrms",
                table: "payroll_loan_deduction",
                column: "installment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payroll_payment_bank_account_id_employee_id",
                schema: "hrms",
                table: "payroll_payment",
                columns: new[] { "bank_account_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "idx_payroll_payment_employee_id",
                schema: "hrms",
                table: "payroll_payment",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_pp_bank",
                schema: "hrms",
                table: "payroll_payment",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_pp_one_live",
                schema: "hrms",
                table: "payroll_payment",
                column: "payroll_transaction_id",
                unique: true,
                filter: "payment_status IN ('pending', 'processed')");

            migrationBuilder.CreateIndex(
                name: "payroll_period_year_month_key",
                schema: "hrms",
                table: "payroll_period",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payroll_run_reverses_run_id",
                schema: "hrms",
                table: "payroll_run",
                column: "reverses_run_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_run_status",
                schema: "hrms",
                table: "payroll_run",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_payroll_run_regular",
                schema: "hrms",
                table: "payroll_run",
                column: "payroll_period_id",
                unique: true,
                filter: "run_type = 'regular' AND status <> 'reversed'");

            migrationBuilder.CreateIndex(
                name: "idx_pt_employee",
                schema: "hrms",
                table: "payroll_transaction",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "payroll_transaction_payroll_run_id_employee_id_key",
                schema: "hrms",
                table: "payroll_transaction",
                columns: new[] { "payroll_run_id", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_payroll_transaction_segment_grade_id",
                schema: "hrms",
                table: "payroll_transaction_segment",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_transaction_segment_pay_scale_stage_id",
                schema: "hrms",
                table: "payroll_transaction_segment",
                column: "pay_scale_stage_id");

            migrationBuilder.CreateIndex(
                name: "idx_payroll_transaction_segment_post_id",
                schema: "hrms",
                table: "payroll_transaction_segment",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "payslip_payroll_transaction_id_key",
                schema: "hrms",
                table: "payslip",
                column: "payroll_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_performance_competency_performance_review_id",
                schema: "hrms",
                table: "performance_competency",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_goal_performance_review_id",
                schema: "hrms",
                table: "performance_goal",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_kpi_performance_review_id",
                schema: "hrms",
                table: "performance_kpi",
                column: "performance_review_id");

            migrationBuilder.CreateIndex(
                name: "idx_performance_review_performance_period_id",
                schema: "hrms",
                table: "performance_review",
                column: "performance_period_id");

            migrationBuilder.CreateIndex(
                name: "idx_pr_evaluator",
                schema: "hrms",
                table: "performance_review",
                column: "evaluator_id");

            migrationBuilder.CreateIndex(
                name: "performance_review_employee_id_performance_period_id_key",
                schema: "hrms",
                table: "performance_review",
                columns: new[] { "employee_id", "performance_period_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pa_employee",
                schema: "hrms",
                table: "position_assignment",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_pa_post_from",
                schema: "hrms",
                table: "position_assignment",
                columns: new[] { "post_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "idx_position_assignment_order_document_id",
                schema: "hrms",
                table: "position_assignment",
                column: "order_document_id");

            migrationBuilder.CreateIndex(
                name: "idx_position_assignment_service_history_id",
                schema: "hrms",
                table: "position_assignment",
                column: "service_history_id");

            migrationBuilder.CreateIndex(
                name: "post_post_code_key",
                schema: "hrms",
                table: "post",
                column: "post_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_post_version_designation_id",
                schema: "hrms",
                table: "post_version",
                column: "designation_id");

            migrationBuilder.CreateIndex(
                name: "idx_post_version_grade_id",
                schema: "hrms",
                table: "post_version",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "idx_post_version_location_id",
                schema: "hrms",
                table: "post_version",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "idx_pv_org_unit",
                schema: "hrms",
                table: "post_version",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_pv_reporting",
                schema: "hrms",
                table: "post_version",
                column: "reporting_post_id");

            migrationBuilder.CreateIndex(
                name: "recruitment_method_name_key",
                schema: "hrms",
                table: "recruitment_method",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "salary_component_component_code_key",
                schema: "hrms",
                table: "salary_component",
                column: "component_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_salary_component_rule_applicable_designation_id",
                schema: "hrms",
                table: "salary_component_rule",
                column: "applicable_designation_id");

            migrationBuilder.CreateIndex(
                name: "idx_salary_component_rule_applicable_org_unit_id",
                schema: "hrms",
                table: "salary_component_rule",
                column: "applicable_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "idx_scr_component_from",
                schema: "hrms",
                table: "salary_component_rule",
                columns: new[] { "salary_component_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "salary_component_rule_salary_component_id_rule_version_key",
                schema: "hrms",
                table: "salary_component_rule",
                columns: new[] { "salary_component_id", "rule_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "service_event_type_name_key",
                schema: "hrms",
                table: "service_event_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tax_slab_tax_year_id_slab_order_key",
                schema: "hrms",
                table: "tax_slab",
                columns: new[] { "tax_year_id", "slab_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tax_year_year_label_key",
                schema: "hrms",
                table: "tax_year",
                column: "year_label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "work_shift_name_key",
                schema: "hrms",
                table: "work_shift",
                column: "name",
                unique: true);

            // exclusion constraints, guard triggers, functions, views and comments EF cannot express
            HrmsSchemaSql.Up(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            HrmsSchemaSql.Down(migrationBuilder);

            migrationBuilder.DropTable(
                name: "attendance_record",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_address",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_contact",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_education",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_emergency_contact",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_family_member",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_pay_record",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_request",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_salary_component",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_separation",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_shift",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_task_update",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_tax_exemption",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_tax_ledger",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "gp_fund_interest_rate",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "gp_fund_transaction",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "holiday_calendar",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "hr_action_request",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "leave_entitlement",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "leave_ledger",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "organization_unit_version",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_adjustment",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_component_detail",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_payment",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payslip",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_competency",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_goal",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_kpi",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "position_assignment",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "post_version",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "tax_slab",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_request_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "work_shift",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_task",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "gp_fund_account",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "leave_application",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "organization_unit_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_loan_deduction",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "salary_component_rule",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_transaction_segment",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_bank_account",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_review",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_service_history",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "location",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "tax_year",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "leave_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "loan_installment_schedule",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "designation",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "pay_scale_stage",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_transaction",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "performance_period",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "service_event_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "organization_unit",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "post",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "recruitment_method",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_document",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee_loan",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "pay_scale_version",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_run",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "document_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "employee",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "loan_type",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "pay_scale_grade",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "payroll_period",
                schema: "hrms");

            migrationBuilder.DropTable(
                name: "salary_component",
                schema: "hrms");
        }
    }
}
