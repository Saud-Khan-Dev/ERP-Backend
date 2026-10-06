using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

public class HrmExceptionHandler(ILogger<HrmExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
  {
    logger.LogError("Error Message : {exceptionMessage}, Time of occurance {time}", exception.Message, DateTime.UtcNow);

    var postgres = FindPostgresException(exception);

    (string Title, string Detail, int StatusCode) details = exception switch
    {
      ValidationException => (
        Title: nameof(ValidationException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status400BadRequest
      ),

      DomainException => (
        Title: nameof(DomainException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status400BadRequest
      ),

      BadHttpRequestException => (
        Title: nameof(BadHttpRequestException),
        Detail: exception.InnerException is JsonException json ? JsonProblem(json) : exception.Message,
        StatusCode: StatusCodes.Status400BadRequest
      ),

      NotFoundException => (
        Title: nameof(NotFoundException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status404NotFound
      ),

      EmployeeProfileRequiredException => (
        Title: nameof(EmployeeProfileRequiredException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status403Forbidden
      ),

      DbUpdateConcurrencyException => (
        Title: nameof(DbUpdateConcurrencyException),
        Detail: "The record was modified by someone else. Reload it and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      // the database refused the change: a constraint or a guard trigger (deferred ones fire at commit, outside
      // DbUpdateException, so the Postgres error is looked for anywhere in the chain)
      _ when postgres is not null => DatabaseRefusal(postgres),

      DbUpdateException => (
        Title: nameof(DbUpdateException),
        Detail: exception.InnerException?.Message ?? exception.Message,
        StatusCode: StatusCodes.Status409Conflict
      ),

      _ => (
        Title: exception.GetType().Name,
        Detail: exception.Message,
        StatusCode: StatusCodes.Status500InternalServerError
      )
    };

    var problemDetails = new ProblemDetails
    {
      Status = details.StatusCode,
      Title = details.Title,
      Detail = details.Detail,
      Instance = context.Request.Path
    };

    problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

    if (exception is ValidationException validationException)
    {
      problemDetails.Extensions.Add(
        "ValidationErrors",
        validationException.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
    }

    context.Response.StatusCode = details.StatusCode;
    await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

    return true;
  }

  /// A body that could not be read: the field (from the JSON path) and what was wrong with it.
  private static string JsonProblem(JsonException error)
  {
    var field = error.Path is { Length: > 2 } path ? path[2..] : null;
    var text = error.Message.StartsWith("'", StringComparison.Ordinal) ? error.Message : "The value has the wrong type or format.";
    return field is null ? text : $"{field}: {text}";
  }

  private static PostgresException? FindPostgresException(Exception exception)
  {
    for (Exception? current = exception; current is not null; current = current.InnerException)
    {
      if (current is PostgresException postgres)
        return postgres;
    }
    return null;
  }

  /// A sentence a person can act on, by the constraint the database named; the database's own message otherwise.
  private static (string Title, string Detail, int StatusCode) DatabaseRefusal(PostgresException error)
  {
    var known = error.ConstraintName is { } name && ConstraintMessages.TryGetValue(name, out var message) ? message : null;

    return error.SqlState switch
    {
      PostgresErrorCodes.UniqueViolation => ("Conflict", known ?? "A record with the same values already exists.", StatusCodes.Status409Conflict),
      PostgresErrorCodes.ExclusionViolation => ("Conflict", known ?? "The period overlaps another one that is already on record.", StatusCodes.Status409Conflict),
      PostgresErrorCodes.ForeignKeyViolation => ("Conflict", known ?? "The record is still referred to elsewhere, or refers to something that does not exist.", StatusCodes.Status409Conflict),
      PostgresErrorCodes.CheckViolation => ("Rejected", known ?? $"The values break the rule '{error.ConstraintName}'.", StatusCodes.Status400BadRequest),
      PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
        ("Conflict", "Someone else changed the same records at the same moment. Try again.", StatusCodes.Status409Conflict),
      // RAISE EXCEPTION from a guard trigger: append-only ledger, locked payroll run, full post ...
      PostgresErrorCodes.RaiseException => ("Rejected", Sentence(error.MessageText), StatusCodes.Status409Conflict),
      _ => ("Conflict", error.MessageText, StatusCodes.Status409Conflict)
    };
  }

  private static string Sentence(string text) =>
      string.IsNullOrWhiteSpace(text) ? "The database refused the change." : char.ToUpperInvariant(text[0]) + text[1..] + (text.EndsWith('.') ? string.Empty : ".");

  private static readonly Dictionary<string, string> ConstraintMessages = new(StringComparer.Ordinal)
  {
    ["employee_employee_number_key"] = "This employee number is already in use.",
    ["employee_cnic_key"] = "An employee with this CNIC is already registered.",
    ["employee_user_id_key"] = "This user account is already linked to another employee.",
    ["organization_unit_code_key"] = "An org unit with this code already exists.",
    ["organization_unit_type_name_key"] = "A unit type with this name already exists.",
    ["organization_unit_type_code_key"] = "A unit type with this code already exists.",
    ["designation_title_key"] = "A designation with this title already exists.",
    ["designation_code_key"] = "A designation with this code already exists.",
    ["post_post_code_key"] = "A post with this code already exists.",
    ["pay_scale_stage_pay_scale_version_id_stage_number_key"] = "Each stage number may appear only once in a pay scale.",
    ["document_type_name_key"] = "A document type with this name already exists.",
    ["recruitment_method_name_key"] = "A recruitment method with this name already exists.",
    ["service_event_type_name_key"] = "A service event type with this name already exists.",
    ["work_shift_name_key"] = "A shift with this name already exists.",
    ["holiday_calendar_holiday_date_name_location_id_key"] = "This holiday is already on the calendar for that date and place.",
    ["attendance_record_employee_id_attendance_date_key"] = "Attendance for this employee on this date is already recorded.",
    ["leave_type_name_key"] = "A leave type with this name already exists.",
    ["leave_entitlement_employee_id_leave_type_id_year_key"] = "This employee already has an entitlement of this leave type for that year.",
    ["salary_component_component_code_key"] = "A salary component with this code already exists.",
    ["salary_component_rule_salary_component_id_rule_version_key"] = "This component already has a rule with that version.",
    ["tax_year_year_label_key"] = "A tax year with this label already exists.",
    ["tax_slab_tax_year_id_slab_order_key"] = "Each slab order may appear only once in a tax year.",
    ["employee_tax_exemption_employee_id_tax_year_id_exemption_ty_key"] = "This exemption is already recorded for the employee in that tax year.",
    ["loan_type_name_key"] = "A loan type with this name already exists.",
    ["gp_fund_account_employee_id_key"] = "The employee already has a GP Fund account.",
    ["gp_fund_account_account_number_key"] = "This GP Fund account number is already in use.",
    ["payroll_period_year_month_key"] = "This payroll month already exists.",
    ["uq_payroll_run_regular"] = "This month already has a regular payroll run. Reverse it before making another.",
    ["payroll_transaction_payroll_run_id_employee_id_key"] = "The employee is already in this payroll run.",
    ["payroll_loan_deduction_installment_id_key"] = "This loan installment has already been deducted in a payroll run.",
    ["uq_pp_one_live"] = "This pay slip already has a pending or processed payment.",
    ["uq_separation_employee"] = "The employee's separation is already recorded.",
    ["performance_review_employee_id_performance_period_id_key"] = "The employee already has a review for this period.",
    ["employee_request_type_name_key"] = "A request type with this name already exists.",
    ["employee_request_type_code_key"] = "A request type with this code already exists.",
    ["uq_employee_address_type"] = "The employee already has an address of this type.",
    ["uq_employee_contact_primary"] = "The employee already has a primary contact of this type.",
    ["uq_eba_one_primary"] = "The employee already has a primary bank account.",
    ["uq_edu_one_highest"] = "The employee already has a highest qualification.",
    ["ex_ouv_no_overlap"] = "The unit already has a version for part of that period.",
    ["ex_psv_no_overlap"] = "The grade already has an active pay scale for part of that period.",
    ["ex_pv_no_overlap"] = "The post already has a version for part of that period.",
    ["ex_epr_no_overlap"] = "The employee already has a pay record for part of that period.",
    ["ex_pa_one_regular"] = "The employee already holds a regular post in that period.",
    ["ex_pp_no_overlap"] = "Another active performance period covers part of those dates.",
    ["ex_es_no_overlap"] = "The employee already has a shift for part of that period.",
    ["ex_la_no_overlap"] = "The employee already has a pending or approved leave in those dates.",
    ["ex_esc_no_overlap"] = "The employee already has an override of this component for part of that period.",
    ["ex_ty_no_overlap"] = "Another tax year covers part of those dates.",
    ["ex_slab_no_overlap"] = "Two slabs of the tax year overlap.",
    ["ex_gir_no_overlap"] = "Another GP Fund interest rate covers part of that period.",
    ["ex_pts_no_overlap"] = "Two segments of the pay slip overlap."
  };
}
