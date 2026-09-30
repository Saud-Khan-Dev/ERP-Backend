using FluentValidation;

public sealed record OutsourcingTermsInput(
  Guid OutsourcingTypeId,
  DateOnly ContractStartDate,
  DateOnly? ContractEndDate = null,
  string? PurposeService = null,
  decimal? ContractAmount = null,
  AmountFrequency? AmountFrequency = null,
  decimal? PerformanceGuarantee = null,
  string? ReferenceNo = null,
  string? Remarks = null)
{
  public async Task<PropertyOutsourcing.Terms> ToTermsAsync(MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<OutsourcingType>(OutsourcingTypeId, cancellationToken),
    ContractStartDate, ContractEndDate, PurposeService, ContractAmount, AmountFrequency, PerformanceGuarantee, ReferenceNo, Remarks);
}

public class OutsourcingTermsInputValidator : AbstractValidator<OutsourcingTermsInput>
{
  public OutsourcingTermsInputValidator()
  {
    RuleFor(x => x.OutsourcingTypeId).NotEmpty();
    RuleFor(x => x.PurposeService).MaximumLength(300);
    RuleFor(x => x.ContractAmount).GreaterThanOrEqualTo(0).When(x => x.ContractAmount.HasValue);
    RuleFor(x => x.AmountFrequency).IsInEnum().When(x => x.AmountFrequency.HasValue);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
  }
}

public sealed record CreateOutsourcingCommandResult(Guid Id, string ContractNo);

/// CON-00001 is generated; the contract starts ACTIVE.
public sealed record CreateOutsourcingCommand(Guid PropertyId, Guid OutsourcedPartyOwnerId, OutsourcingTermsInput Terms) : ICommand<Result<CreateOutsourcingCommandResult>>;

public class CreateOutsourcingCommandValidator : AbstractValidator<CreateOutsourcingCommand>
{
  public CreateOutsourcingCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.OutsourcedPartyOwnerId).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new OutsourcingTermsInputValidator());
  }
}
