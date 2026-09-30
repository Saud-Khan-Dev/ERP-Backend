using FluentValidation;

public sealed record AppealInput(
  Guid AppellantOwnerId,
  string AppealedOrderRef,
  DateOnly AppealedOrderDate,
  DateOnly AppealDate,
  DateOnly? OrderReceivedDate = null,
  AppealOrderSource? OrderSourceTable = null,
  Guid? OrderSourceId = null,
  string? AppellateAuthority = null,
  string? DelegatedOfficer = null,
  string? Remarks = null);

public class AppealInputValidator : AbstractValidator<AppealInput>
{
  public AppealInputValidator()
  {
    RuleFor(x => x.AppellantOwnerId).NotEmpty();
    RuleFor(x => x.AppealedOrderRef).NotEmpty().MaximumLength(100);
    RuleFor(x => x.OrderSourceTable).IsInEnum().When(x => x.OrderSourceTable.HasValue);
    RuleFor(x => x.AppellateAuthority).MaximumLength(150);
    RuleFor(x => x.DelegatedOfficer).MaximumLength(150);
  }
}

/// Warnings: rule 6 — filed more than 30 days after the order was received (not refused).
public sealed record FileAppealCommandResult(Guid Id, string AppealNo, DateOnly DecisionDueDate, IReadOnlyList<string> Warnings);

/// A departmental appeal to the Chief Secretary (Act s.32). APL-00001 is generated;
/// decision_due_date = appeal_date + 120 days.
public sealed record FileAppealCommand(Guid PropertyId, AppealInput Appeal) : ICommand<Result<FileAppealCommandResult>>;

public class FileAppealCommandValidator : AbstractValidator<FileAppealCommand>
{
  public FileAppealCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Appeal).NotNull().SetValidator(new AppealInputValidator());
  }
}

public static class AppealInputs
{
  public static async Task<PropertyAppeal.Filing> ToFilingAsync(this AppealInput input, IApplicationDbContext context, CancellationToken cancellationToken) => new(
    await context.LoadOwnerAsync(input.AppellantOwnerId, cancellationToken),
    input.AppealedOrderRef, input.AppealedOrderDate, input.OrderReceivedDate, input.OrderSourceTable, input.OrderSourceId,
    input.AppealDate, input.AppellateAuthority, input.DelegatedOfficer, input.Remarks);
}
