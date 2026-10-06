using FluentValidation;

public sealed record OrgUnitDetailsInput(string Name, Guid UnitTypeId, Guid? ParentUnitId, Guid? LocationId, Guid? HeadPostId);

/// A new org unit and its first version. The code never changes afterwards.
public sealed record CreateOrgUnitCommand(string Code, OrgUnitDetailsInput Details, DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;

/// A restructuring from a date (rename, new parent, type, location or head post): the current version closes the day before.
public sealed record RestructureOrgUnitCommand(Guid Id, OrgUnitDetailsInput Details, DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;

/// Fixes a mistake in the latest version without starting a new period.
public sealed record CorrectOrgUnitCommand(Guid Id, OrgUnitDetailsInput Details) : ICommand<Result<UpdatedResult>>;

/// The unit stops (or starts again) from a date. Its history stays.
public sealed record SetOrgUnitActivationCommand(Guid Id, bool IsActive, DateOnly EffectiveFrom) : ICommand<Result<CreatedResult>>;

public class OrgUnitDetailsInputValidator : AbstractValidator<OrgUnitDetailsInput>
{
  public OrgUnitDetailsInputValidator()
  {
    RuleFor(x => x.Name).NotEmpty().MaximumLength(OrganizationUnitVersion.NameMaxLength);
    RuleFor(x => x.UnitTypeId).NotEmpty();
  }
}

public class CreateOrgUnitCommandValidator : AbstractValidator<CreateOrgUnitCommand>
{
  public CreateOrgUnitCommandValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(OrganizationUnit.CodeMaxLength);
    RuleFor(x => x.Details).NotNull().SetValidator(new OrgUnitDetailsInputValidator());
    RuleFor(x => x.Details.HeadPostId).Null().When(x => x.Details is not null)
      .WithMessage("A new unit has no posts yet; set the head post once its posts are sanctioned.");
  }
}

public class RestructureOrgUnitCommandValidator : AbstractValidator<RestructureOrgUnitCommand>
{
  public RestructureOrgUnitCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new OrgUnitDetailsInputValidator());
  }
}

public class CorrectOrgUnitCommandValidator : AbstractValidator<CorrectOrgUnitCommand>
{
  public CorrectOrgUnitCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Details).NotNull().SetValidator(new OrgUnitDetailsInputValidator());
  }
}
