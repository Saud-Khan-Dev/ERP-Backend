using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// Links an account to the person's HR record (HRM's staff directory), taking HR's employee number as the account's
/// code - or unlinks it (both null). HRM keeps its own side of the link (`POST /employees/{id}/user`); the screens call
/// both. The `emp` claim follows on the person's next token.
public sealed record LinkEmployeeCommand(Guid UserId, Guid? EmployeeId, string? EmployeeCode) : ICommand<Result<LinkEmployeeCommandResult>>;

public sealed record LinkEmployeeCommandResult(Guid? EmployeeId, string? EmployeeCode);

public class LinkEmployeeCommandValidator : AbstractValidator<LinkEmployeeCommand>
{
  public LinkEmployeeCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.EmployeeId).Must(id => id != Guid.Empty).WithMessage("Choose the person's HR record.");
    RuleFor(x => x.EmployeeCode).NotEmpty().When(x => x.EmployeeId is not null)
      .WithMessage("The HR employee number is missing - choose the person again.");
    RuleFor(x => x.EmployeeCode).MaximumLength(EmployeeCode.MaxLength);
  }
}

public class LinkEmployeeHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity)
  : ICommandHandler<LinkEmployeeCommand, Result<LinkEmployeeCommandResult>>
{
  public async Task<Result<LinkEmployeeCommandResult>> Handle(LinkEmployeeCommand command, CancellationToken cancellationToken)
  {
    var userId = UserId.Of(command.UserId);
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");
    // linking your own account to an HR record would make you that employee in self-service (payslips, CNIC, leave)
    guard.EnsureNotSelf(userId, "link an HR record to");
    await guard.EnsureCanActOnAsync(userId, "link", cancellationToken);

    var code = command.EmployeeId is null || string.IsNullOrWhiteSpace(command.EmployeeCode) ? null : EmployeeCode.Of(command.EmployeeCode);
    if (user.EmployeeId == command.EmployeeId && user.EmployeeCode == code)
      return Result<LinkEmployeeCommandResult>.Success(new LinkEmployeeCommandResult(user.EmployeeId, user.EmployeeCode?.Value));

    if (command.EmployeeId is { } employeeId)
    {
      var other = await context.Users.IgnoreQueryFilters().AsNoTracking()
        .Where(u => u.Id != userId && u.EmployeeId == employeeId)
        .Select(u => new { Name = u.DisplayName.Value, Username = u.Username.Value, u.DeletedAt })
        .FirstOrDefaultAsync(cancellationToken);
      if (other is not null)
        return Result<LinkEmployeeCommandResult>.Failure(other.DeletedAt is null
          ? $"This HR record is already linked to the account of {other.Name} ({other.Username}) - unlink it there first."
          : $"This HR record is linked to the deleted account {other.Username} - restore and unlink that account first.");
    }

    var before = user.EmployeeCode?.Value;
    user.LinkEmployee(command.EmployeeId, code);

    await activity.RecordAsync(
      command.EmployeeId is null ? ActivityAction.EmployeeUnlinked : ActivityAction.EmployeeLinked,
      ActivityTargetType.User, user.Id.Value, user.DisplayName.Value,
      command.EmployeeId is null ? (before is null ? null : $"Was {before}") : $"HR record {code!.Value}",
      cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<LinkEmployeeCommandResult>.Success(new LinkEmployeeCommandResult(user.EmployeeId, user.EmployeeCode?.Value));
  }
}
