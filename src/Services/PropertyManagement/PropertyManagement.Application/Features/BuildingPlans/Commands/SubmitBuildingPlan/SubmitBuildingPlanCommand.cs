using FluentValidation;

public sealed record BuildingPlanInput(
  Guid BuildingPlanTypeId,
  Guid? ApplicantOwnerId = null,
  DateOnly? SubmissionDate = null,
  decimal? CoveredArea = null,
  Guid? MeasurementUnitId = null,
  int? Floors = null,
  string? ArchitectName = null,
  string? Remarks = null);

public class BuildingPlanInputValidator : AbstractValidator<BuildingPlanInput>
{
  public BuildingPlanInputValidator()
  {
    RuleFor(x => x.BuildingPlanTypeId).NotEmpty();
    RuleFor(x => x.CoveredArea).GreaterThan(0).When(x => x.CoveredArea.HasValue);
    RuleFor(x => x.MeasurementUnitId).NotEmpty().When(x => x.CoveredArea.HasValue).WithMessage("A covered area needs its measurement unit.");
    RuleFor(x => x.Floors).GreaterThanOrEqualTo(0).When(x => x.Floors.HasValue);
    RuleFor(x => x.ArchitectName).MaximumLength(150);
  }
}

public static class BuildingPlanInputs
{
  public static async Task<BuildingPlan.Details> ToDetailsAsync(this BuildingPlanInput input, IApplicationDbContext context, MasterLookup masters, CancellationToken cancellationToken) => new(
    await masters.GetAsync<BuildingPlanType>(input.BuildingPlanTypeId, cancellationToken),
    input.ApplicantOwnerId is { } applicantId ? await context.LoadOwnerAsync(applicantId, cancellationToken) : null,
    input.SubmissionDate,
    input.CoveredArea,
    await masters.GetOptionalAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken),
    input.Floors, input.ArchitectName, input.Remarks);
}

public sealed record SubmitBuildingPlanCommandResult(Guid Id, string PlanNo, int RevisionNo);

/// BP-00001 is generated; the plan starts SUBMITTED at revision 0. The drawing is uploaded as a
/// document with entityType = BuildingPlan.
public sealed record SubmitBuildingPlanCommand(Guid PropertyId, BuildingPlanInput Plan) : ICommand<Result<SubmitBuildingPlanCommandResult>>;

public class SubmitBuildingPlanCommandValidator : AbstractValidator<SubmitBuildingPlanCommand>
{
  public SubmitBuildingPlanCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Plan).NotNull().SetValidator(new BuildingPlanInputValidator());
  }
}
