using Microsoft.EntityFrameworkCore;

public class CreateAttributeGroupHandler(IApplicationDbContext context)
  : ICommandHandler<CreateAttributeGroupCommand, Result<CreateAttributeGroupCommandResult>>
{
  public async Task<Result<CreateAttributeGroupCommandResult>> Handle(CreateAttributeGroupCommand command, CancellationToken cancellationToken)
  {
    var input = command.Group;
    var code = LookupCode.Of(input.Code);

    if (await context.AttributeGroups.AnyAsync(g => g.Code == code, cancellationToken))
      return Result<CreateAttributeGroupCommandResult>.Failure($"An attribute group with code {code.Value} already exists.");

    var group = AttributeGroup.Create(
      AttributeGroupId.Of(Guid.NewGuid()),
      code,
      Name.Of(input.Name, 150),
      input.Description,
      input.DisplayOrder,
      input.IsCollapsible);

    if (!input.IsActive)
      group.Update(code, Name.Of(input.Name, 150), input.Description, input.DisplayOrder, input.IsCollapsible, false);

    await context.AttributeGroups.AddAsync(group, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateAttributeGroupCommandResult>.Success(new CreateAttributeGroupCommandResult(group.Id.Value));
  }
}
