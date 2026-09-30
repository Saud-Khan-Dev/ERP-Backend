using FluentValidation;

/// The code is not editable: it is what the application and reports compare against.
public sealed record UpdateMasterInput(
  string Name,
  string? Description = null,
  int SortOrder = 0,
  decimal? FactorToBase = null,
  bool? IsBase = null,
  string? StorageFolder = null,
  bool? RequiresRelationship = null);

public sealed record UpdateMasterCommandResult(bool IsSuccess);

public sealed record UpdateMasterCommand(string Type, Guid Id, UpdateMasterInput Item) : ICommand<Result<UpdateMasterCommandResult>>;

public class UpdateMasterCommandValidator : AbstractValidator<UpdateMasterCommand>
{
  public UpdateMasterCommandValidator()
  {
    RuleFor(x => x.Type).NotEmpty();
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Item).NotNull();
    RuleFor(x => x.Item.Name).NotEmpty().MaximumLength(MasterData.NameMaxLength);
    RuleFor(x => x.Item.Description).MaximumLength(2000);
    RuleFor(x => x.Item.FactorToBase).GreaterThan(0).When(x => x.Item.FactorToBase.HasValue);
  }
}
