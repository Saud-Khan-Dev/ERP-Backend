using FluentValidation;

public sealed record UpdateAttributeGroupCommandResult(bool IsSuccess);

public sealed record UpdateAttributeGroupCommand(Guid Id, AttributeGroupInput Group) : ICommand<Result<UpdateAttributeGroupCommandResult>>;

public class UpdateAttributeGroupCommandValidator : AbstractValidator<UpdateAttributeGroupCommand>
{
  public UpdateAttributeGroupCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Group).NotNull().SetValidator(new AttributeGroupInputValidator());
  }
}
