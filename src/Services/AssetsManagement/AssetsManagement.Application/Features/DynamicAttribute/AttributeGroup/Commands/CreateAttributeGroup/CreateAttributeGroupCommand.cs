using FluentValidation;

public sealed record AttributeGroupInput(string Code, string Name, string? Description, int? DisplayOrder = null, bool IsCollapsible = true, bool IsActive = true);

public sealed record CreateAttributeGroupCommandResult(Guid Id);

public sealed record CreateAttributeGroupCommand(AttributeGroupInput Group) : ICommand<Result<CreateAttributeGroupCommandResult>>;

public class AttributeGroupInputValidator : AbstractValidator<AttributeGroupInput>
{
  public AttributeGroupInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
  }
}

public class CreateAttributeGroupCommandValidator : AbstractValidator<CreateAttributeGroupCommand>
{
  public CreateAttributeGroupCommandValidator()
  {
    RuleFor(x => x.Group).NotNull().SetValidator(new AttributeGroupInputValidator());
  }
}
