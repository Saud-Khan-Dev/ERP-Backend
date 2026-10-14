using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

public class TaxationExceptionHandler(ILogger<TaxationExceptionHandler> logger) : IExceptionHandler
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

      // Property or HRM did not answer, or refused the signed-in user
      ExternalServiceException => (
        Title: nameof(ExternalServiceException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status503ServiceUnavailable
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
      // RAISE EXCEPTION from a database function
      PostgresErrorCodes.RaiseException => ("Rejected", Sentence(error.MessageText), StatusCodes.Status409Conflict),
      _ => ("Conflict", error.MessageText, StatusCodes.Status409Conflict)
    };
  }

  private static string Sentence(string text) =>
      string.IsNullOrWhiteSpace(text) ? "The database refused the change." : char.ToUpperInvariant(text[0]) + text[1..] + (text.EndsWith('.') ? string.Empty : ".");

  private static readonly Dictionary<string, string> ConstraintMessages = new(StringComparer.Ordinal)
  {
    ["tax_circle_code_key"] = "A circle with this code already exists.",
    ["tax_circle_town_town_id_effective_from_key"] = "The town already has a circle assignment from that date.",
    ["tax_head_code_key"] = "A tax head with this code already exists.",
    ["category_map_property_type_id_key"] = "This property type is already mapped to a tax category.",
    ["ltf_factor_rate_set_id_town_id_tax_category_component_band_key"] = "Two locality factor bands of the town start at the same value.",
    ["tax_setting_key_effective_from_key"] = "The setting already has a value from that date.",
    ["special_policy_code_key"] = "A special policy with this code already exists.",
    ["penalty_rule_code_key"] = "A penalty rule with this code already exists.",
    ["notice_template_stage_key"] = "This notice stage already has a template.",
    ["notice_table_column_layout_id_column_key_key"] = "A column appears twice in the notice table.",
    ["tax_file_file_no_key"] = "This file number is already in use. Try again.",
    ["tax_file_property_id_key"] = "The property already has a tax file.",
    ["tax_file_psid_key"] = "This 1Link reference is already in use. Try again.",
    ["tax_file_head_value_tax_file_id_head_id_key"] = "The file already has a value for this head.",
    ["tax_file_override_tax_file_id_head_id_effective_from_key"] = "The head already has an override from that date.",
    ["demand_demand_no_key"] = "This demand number is already in use. Try again.",
    ["demand_tax_file_id_fiscal_year_key"] = "The file already has a demand for that year.",
    ["notice_notice_no_key"] = "This notice number is already in use. Try again.",
    ["notice_endst_no_key"] = "Another notice took the same endorsement number at the same moment. Try again.",
    ["payment_transaction_ref_key"] = "This transaction reference was already posted.",
    ["fee_fee_no_key"] = "This fee number is already in use. Try again.",
    ["clearance_clearance_no_key"] = "This clearance number is already in use. Try again."
  };
}
