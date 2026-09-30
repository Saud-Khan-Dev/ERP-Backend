using FluentValidation;

public sealed record LeaseTermsInput(
  Guid LeaseTypeId,
  DateOnly LeaseStartDate,
  DateOnly LeaseEndDate,
  int? LeaseTermYears = null,
  string? LeasePurpose = null,
  decimal? LeaseAmount = null,
  AmountFrequency? AmountFrequency = null,
  decimal? SecurityDeposit = null,
  string? AgreementReference = null,
  DateOnly? AgreementDate = null,
  bool IsRenewable = false,
  string? Remarks = null)
{
  public async Task<PropertyLease.Terms> ToTermsAsync(MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<LeaseType>(LeaseTypeId, cancellationToken),
    LeaseStartDate, LeaseEndDate, LeaseTermYears, LeasePurpose, LeaseAmount, AmountFrequency, SecurityDeposit,
    AgreementReference, AgreementDate, IsRenewable, Remarks);
}

public class LeaseTermsInputValidator : AbstractValidator<LeaseTermsInput>
{
  public LeaseTermsInputValidator()
  {
    RuleFor(x => x.LeaseTypeId).NotEmpty();
    RuleFor(x => x.LeaseEndDate).GreaterThan(x => x.LeaseStartDate).WithMessage("The lease must end after it starts.");
    RuleFor(x => x.LeasePurpose).MaximumLength(200);
    RuleFor(x => x.LeaseAmount).GreaterThanOrEqualTo(0).When(x => x.LeaseAmount.HasValue);
    RuleFor(x => x.AmountFrequency).IsInEnum().When(x => x.AmountFrequency.HasValue);
    RuleFor(x => x.AgreementReference).MaximumLength(100);
  }
}
