using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class IdentityExceptionHandler(ILogger<IdentityExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
  {
    // the real reason for an authentication failure is logged here and nowhere else
    if (exception is InvalidCredentialsException invalid)
      logger.LogWarning("Authentication failed: {Reason}, path {Path}", invalid.Reason, context.Request.Path);
    else
      logger.LogError("Error Message : {exceptionMessage}, Time of occurance {time}", exception.Message, DateTime.UtcNow);

    (string Title, string Detail, int StatusCode) details = exception switch
    {
      // deliberately generic: never confirm whether a username exists
      InvalidCredentialsException => (
        Title: "AuthenticationFailed",
        Detail: InvalidCredentialsException.GenericMessage,
        StatusCode: StatusCodes.Status401Unauthorized
      ),

      AccountUnavailableException => (
        Title: nameof(AccountUnavailableException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status403Forbidden
      ),

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
        Detail: exception.Message,
        StatusCode: StatusCodes.Status400BadRequest
      ),

      NotFoundException => (
        Title: nameof(NotFoundException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status404NotFound
      ),

      DbUpdateConcurrencyException => (
        Title: nameof(DbUpdateConcurrencyException),
        Detail: "The record was modified by someone else. Reload it and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      // the database's own text names tables and constraints - it goes to the log, never to the screen
      DbUpdateException => (
        Title: nameof(DbUpdateException),
        Detail: "This change clashes with a record saved meanwhile (the same username, email or link). Reload and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      _ => (
        Title: "ServerError",
        Detail: "Something went wrong on the server. Try again; if it keeps happening, tell your administrator the time it happened.",
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
}
