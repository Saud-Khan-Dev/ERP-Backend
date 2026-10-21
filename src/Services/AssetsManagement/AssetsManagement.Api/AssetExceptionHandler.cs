using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class AssetExceptionHandler(ILogger<AssetExceptionHandler> logger) : IExceptionHandler
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

      // the endpoint's permission is held, but not the one this particular change needs
      AccessDeniedException => (
        Title: "Forbidden",
        Detail: exception.Message,
        StatusCode: StatusCodes.Status403Forbidden
      ),

      // HRM did not answer, or refused the signed-in user
      ExternalServiceException => (
        Title: nameof(ExternalServiceException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status503ServiceUnavailable
      ),

      DbUpdateConcurrencyException => (
        Title: nameof(DbUpdateConcurrencyException),
        Detail: "The record was changed by someone else at the same moment. Reload it and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      // the database refused the change: a sentence by the constraint it named, never Postgres' own text
      _ when postgres is not null => DatabaseRefusal(postgres),

      DbUpdateException => (
        Title: nameof(DbUpdateException),
        Detail: "The change could not be saved because it conflicts with records already on file. Reload and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      // a value object refused a missing or too long value that no validator caught
      ArgumentException => (
        Title: "BadRequest",
        Detail: "A value is missing or too long. Check the form and try again.",
        StatusCode: StatusCodes.Status400BadRequest
      ),

      _ => (
        Title: exception.GetType().Name,
        Detail: "Something went wrong on the server. Nothing was saved - try again.",
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
    const string text = "The value has the wrong type or format.";
    return field is null ? text : $"{field}: {text}";
  }

  private static PostgresException? FindPostgresException(Exception exception)
  {
    for (Exception? current = exception; current is not null; current = current.InnerException)
      if (current is PostgresException postgres)
        return postgres;
    return null;
  }

  private static (string Title, string Detail, int StatusCode) DatabaseRefusal(PostgresException error)
  {
    var known = error.ConstraintName is { } name && ConstraintMessages.TryGetValue(name, out var message) ? message : null;

    return error.SqlState switch
    {
      PostgresErrorCodes.UniqueViolation => ("Conflict", known ?? "A record with the same values already exists.", StatusCodes.Status409Conflict),
      PostgresErrorCodes.ForeignKeyViolation => ("Conflict", known ?? "The record is still used elsewhere, or refers to something that no longer exists.", StatusCodes.Status409Conflict),
      PostgresErrorCodes.CheckViolation => ("Rejected", known ?? "The values break one of the register's rules. Check the dates and amounts.", StatusCodes.Status400BadRequest),
      PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
        ("Conflict", "Someone else changed the same records at the same moment. Try again.", StatusCodes.Status409Conflict),
      _ => ("Conflict", known ?? "The change could not be saved. Reload and try again.", StatusCodes.Status409Conflict)
    };
  }

  private static readonly Dictionary<string, string> ConstraintMessages = new(StringComparer.Ordinal)
  {
    ["ix_asset_asset_code"] = "This asset tag is already in use. Try again - a new tag will be issued.",
    ["ix_asset_barcode"] = "This barcode is already used by another asset.",
    ["ux_asset_depreciation_schedule_active"] = "The asset already has an active depreciation schedule.",
    ["ix_asset_acquisition_asset_id"] = "The asset already has a purchase record.",
    ["ix_asset_disposal_asset_id"] = "The asset has already been disposed of.",
    ["ix_asset_valuation_history_asset_id_valuation_date"] = "The asset already has a valuation on that date.",
    ["ix_asset_depreciation_entry_schedule_id_period_start_period_end"] = "That depreciation period is already on the schedule.",
    ["ck_asset_acquisition_warranty"] = "The warranty cannot end before the purchase date.",
    ["ck_asset_acquisition_cost"] = "The cost cannot be negative."
  };
}
