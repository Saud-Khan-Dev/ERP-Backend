using Microsoft.EntityFrameworkCore.Migrations;

/// Raw SQL of the PeopleRulesAndLeaveDecisions migration (2026-10-20). HrmsSchemaSql is never edited after it shipped;
/// later SQL lives in classes like this one, each called from its own migration.
///
/// - New enum labels are added in the C# declaration order (Npgsql's own diff places them by alphabetical neighbours,
///   which would scramble the order Postgres sorts them in). Labels cannot be dropped, so Down leaves them.
/// - The seat check counts the most regular holders on any one day of the new holding instead of every holding that
///   overlaps it, so holders who follow one another share a seat (back-dated placements, mutual transfers).
/// - employee_change_log is append-only like the other ledgers.
public static class PeopleRulesSql
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
    "ALTER TYPE hrms.gender_enum ADD VALUE IF NOT EXISTS 'transgender';",

    "ALTER TYPE hrms.employment_status_enum ADD VALUE IF NOT EXISTS 'dismissed';",
    "ALTER TYPE hrms.employment_status_enum ADD VALUE IF NOT EXISTS 'removed';",
    "ALTER TYPE hrms.employment_status_enum ADD VALUE IF NOT EXISTS 'contract_ended';",
    "ALTER TYPE hrms.employment_status_enum ADD VALUE IF NOT EXISTS 'repatriated';",

    "ALTER TYPE hrms.qualification_level_enum ADD VALUE IF NOT EXISTS 'primary' BEFORE 'matric';",
    "ALTER TYPE hrms.qualification_level_enum ADD VALUE IF NOT EXISTS 'middle' BEFORE 'matric';",
    "ALTER TYPE hrms.qualification_level_enum ADD VALUE IF NOT EXISTS 'diploma' AFTER 'intermediate';",

    "ALTER TYPE hrms.separation_type_enum ADD VALUE IF NOT EXISTS 'compulsory_retirement';",
    "ALTER TYPE hrms.separation_type_enum ADD VALUE IF NOT EXISTS 'premature_retirement';",
    "ALTER TYPE hrms.separation_type_enum ADD VALUE IF NOT EXISTS 'removal_from_service';",

    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'dismissal';",
    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'removal_from_service';",
    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'compulsory_retirement';",
    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'premature_retirement';",
    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'end_of_contract';",
    "ALTER TYPE hrms.hr_action_type_enum ADD VALUE IF NOT EXISTS 'deputation_end';",

    // ---- post capacity: the most holders on any one day, at commit, with the post row locked ----
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

      -- the busiest day of this holding: its first day or a day another holding starts inside it
      SELECT coalesce(max(x.cnt), 0) INTO v_taken
        FROM (
          SELECT (SELECT count(*) FROM hrms.position_assignment pa
                   WHERE pa.post_id = v_row.post_id AND pa.assignment_type = 'regular' AND pa.status = 'active'
                     AND pa.id <> v_row.id
                     AND d.day BETWEEN pa.effective_from AND coalesce(pa.effective_to, 'infinity'::date)) AS cnt
            FROM (
              SELECT v_row.effective_from AS day
              UNION
              SELECT pa.effective_from
                FROM hrms.position_assignment pa
               WHERE pa.post_id = v_row.post_id AND pa.assignment_type = 'regular' AND pa.status = 'active' AND pa.id <> v_row.id
                 AND pa.effective_from BETWEEN v_row.effective_from AND coalesce(v_row.effective_to, 'infinity'::date)
            ) d
        ) x;
      IF v_taken >= v_seats THEN
        RAISE EXCEPTION 'post % is full: % of % seats already held in the requested period', v_row.post_id, v_taken, v_seats;
      END IF;
      RETURN NULL;
    END $$;
    """,

    "CREATE TRIGGER trg_ecl_append_only BEFORE UPDATE OR DELETE ON hrms.employee_change_log FOR EACH ROW EXECUTE FUNCTION hrms.fn_block_mutation();",

    "COMMENT ON TABLE hrms.employee_change_log IS 'Append-only trail of changes to an employee''s identity, employment, sign-in account and pay: old -> new, reason, who and when.';"
  ];

  private static readonly string[] DownStatements =
  [
    "DROP TRIGGER IF EXISTS trg_ecl_append_only ON hrms.employee_change_log;"
    // the seat check keeps its new form and the enum labels stay: Postgres cannot drop a label that rows may use
  ];
}
