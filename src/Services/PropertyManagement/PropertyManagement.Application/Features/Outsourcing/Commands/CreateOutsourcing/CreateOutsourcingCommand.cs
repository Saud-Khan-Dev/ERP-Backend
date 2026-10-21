using FluentValidation;

/// MoneyDirection: Receivable (the contractor pays GDA - a concession, rent-roll income) or Payable (GDA pays the
/// contractor - a service, an expense). Required on a new contract.
public sealed record OutsourcingTermsInput(
  Guid OutsourcingTypeId,
  DateOnly ContractStartDate,
  DateOnly? ContractEndDate = null,
  string? PurposeService = null,
  decimal? ContractAmount = null,
  AmountFrequency? AmountFrequency = null,
  decimal? PerformanceGuarantee = null,
  string? ReferenceNo = null,
  string? Remarks = null,
  MoneyDirection? MoneyDirection = null)
{
  public async Task<PropertyOutsourcing.Terms> ToTermsAsync(MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<OutsourcingType>(OutsourcingTypeId, cancellationToken),
    ContractStartDate, ContractEndDate, PurposeService, ContractAmount, AmountFrequency, PerformanceGuarantee, ReferenceNo, Remarks, MoneyDirection);
}

public class OutsourcingTermsInputValidator : AbstractValidator<OutsourcingTermsInput>
{
  public OutsourcingTermsInputValidator()
  {
    RuleFor(x => x.OutsourcingTypeId).NotEmpty();
    RuleFor(x => x.PurposeService).MaximumLength(300);
    RuleFor(x => x.ContractAmount).GreaterThanOrEqualTo(0).When(x => x.ContractAmount.HasValue);
    RuleFor(x => x.AmountFrequency).IsInEnum().When(x => x.AmountFrequency.HasValue);
    RuleFor(x => x.MoneyDirection).IsInEnum().When(x => x.MoneyDirection.HasValue);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.ContractEndDate).GreaterThanOrEqualTo(x => x.ContractStartDate).When(x => x.ContractEndDate.HasValue)
      .WithMessage("Contract end date cannot be before Contract start date.");
    RuleFor(x => x.PerformanceGuarantee).GreaterThanOrEqualTo(0).When(x => x.PerformanceGuarantee.HasValue);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

/// Warnings: other agreements running on the property at the same time.
public sealed record CreateOutsourcingCommandResult(Guid Id, string ContractNo, IReadOnlyList<string> Warnings);

/// CON-00001 is generated; the contract starts ACTIVE, or as a DRAFT (terms still editable) with AsDraft.
public sealed record CreateOutsourcingCommand(Guid PropertyId, Guid OutsourcedPartyOwnerId, OutsourcingTermsInput Terms, bool AsDraft = false) : ICommand<Result<CreateOutsourcingCommandResult>>;

public class CreateOutsourcingCommandValidator : AbstractValidator<CreateOutsourcingCommand>
{
  public CreateOutsourcingCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.OutsourcedPartyOwnerId).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new OutsourcingTermsInputValidator());
    RuleFor(x => x.Terms.MoneyDirection).NotNull()
      .WithMessage("Say who pays whom: the contractor pays GDA (income), or GDA pays the contractor (an expense).");
  }
}
