using Microsoft.EntityFrameworkCore;

public class UpdateAttributeGroupHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAttributeGroupCommand, Result<UpdateAttributeGroupCommandResult>>
{
  public async Task<Result<UpdateAttributeGroupCommandResult>> Handle(UpdateAttributeGroupCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeGroupId.Of(command.Id);
    var group = await context.AttributeGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
      ?? throw new AttributeGroupNotFoundException($"Attribute group {command.Id} was not found.");

    var input = command.Group;
    var code = LookupCode.Of(input.Code);

    if (await context.AttributeGroups.AnyAsync(g => g.Id != id && g.Code == code, cancellationToken))
      return Result<UpdateAttributeGroupCommandResult>.Failure($"An attribute group with code {code.Value} already exists.");

    group.Update(code, Name.Of(input.Name, 150), input.Description, input.DisplayOrder, input.IsCollapsible, input.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateAttributeGroupCommandResult>.Success(new UpdateAttributeGroupCommandResult(true));
  }
}
