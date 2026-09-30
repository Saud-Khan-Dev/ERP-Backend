using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class PropertyExceptionHandler(ILogger<PropertyExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
  {
    logger.LogError("Error Message : {exceptionMessage}, Time of occurance {time}", exception.Message, DateTime.UtcNow);

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

      DbUpdateConcurrencyException => (
        Title: nameof(DbUpdateConcurrencyException),
        Detail: "The record was modified by someone else. Reload it and try again.",
        StatusCode: StatusCodes.Status409Conflict
      ),

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
}
