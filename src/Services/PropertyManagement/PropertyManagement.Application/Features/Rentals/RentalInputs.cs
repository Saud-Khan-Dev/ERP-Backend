using FluentValidation;

public sealed record RentalTermsInput(
  Guid RentalTypeId,
  DateOnly RentalStartDate,
  decimal RentAmount,
  RentFrequency RentFrequency = RentFrequency.Monthly,
  DateOnly? RentalEndDate = null,
  decimal? SecurityDeposit = null,
  decimal? AnnualIncreasePct = null,
  string? AgreementReference = null,
  DateOnly? AgreementDate = null,
  string? Remarks = null)
{
  public async Task<PropertyRental.Terms> ToTermsAsync(MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<RentalType>(RentalTypeId, cancellationToken),
    RentalStartDate, RentalEndDate, RentAmount, RentFrequency, SecurityDeposit, AnnualIncreasePct, AgreementReference, AgreementDate, Remarks);
}

public class RentalTermsInputValidator : AbstractValidator<RentalTermsInput>
{
  public RentalTermsInputValidator()
  {
    RuleFor(x => x.RentalTypeId).NotEmpty();
    RuleFor(x => x.RentAmount).GreaterThanOrEqualTo(0);
    RuleFor(x => x.RentFrequency).IsInEnum();
    RuleFor(x => x.AnnualIncreasePct).InclusiveBetween(0, 999.99m).When(x => x.AnnualIncreasePct.HasValue);
    RuleFor(x => x.AgreementReference).MaximumLength(100);
  }
}
