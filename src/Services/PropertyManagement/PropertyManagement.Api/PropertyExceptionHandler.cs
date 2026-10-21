using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class PropertyExceptionHandler(ILogger<PropertyExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
  {
    logger.LogError(exception, "Request {path} failed ({trace}): {message} {inner}", context.Request.Path, context.TraceIdentifier,
      exception.Message, exception.InnerException?.Message);

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
        Detail: exception.Message,
        StatusCode: StatusCodes.Status400BadRequest
      ),

      NotFoundException => (
        Title: nameof(NotFoundException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status404NotFound
      ),

      AuthorizedOfficerRequiredException => (
        Title: nameof(AuthorizedOfficerRequiredException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status403Forbidden
      ),

      PermissionRequiredException => (
        Title: nameof(PermissionRequiredException),
        Detail: exception.Message,
        StatusCode: StatusCodes.Status403Forbidden
      ),

      DbUpdateConcurrencyException => (
        Title: nameof(DbUpdateConcurrencyException),
        Detail: "Someone else changed this property's records at the same moment, so nothing was saved. Reload the page and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      // the database's own text (constraint names, SQL) never reaches the clerk; it is in the log
      DbUpdateException => (
        Title: nameof(DbUpdateException),
        Detail: "The change could not be saved because it clashes with a record already on file (an entry for the same day or code). Reload the page and check.",
        StatusCode: StatusCodes.Status409Conflict
      ),

      _ => (
        Title: "ServerError",
        Detail: $"Something went wrong on the server and nothing was saved. Try again; if it keeps happening, tell the administrator (reference {context.TraceIdentifier}).",
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
