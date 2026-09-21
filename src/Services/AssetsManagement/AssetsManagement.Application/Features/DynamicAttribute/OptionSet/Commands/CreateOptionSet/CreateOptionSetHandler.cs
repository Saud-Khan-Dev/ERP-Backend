using Microsoft.EntityFrameworkCore;

public class CreateOptionSetHandler(IApplicationDbContext context)
  : ICommandHandler<CreateOptionSetCommand, Result<CreateOptionSetCommandResult>>
{
  public async Task<Result<CreateOptionSetCommandResult>> Handle(CreateOptionSetCommand command, CancellationToken cancellationToken)
  {
    var input = command.OptionSet;
    var code = LookupCode.Of(input.Code);

    if (await context.OptionSets.AnyAsync(o => o.Code == code, cancellationToken))
      return Result<CreateOptionSetCommandResult>.Failure($"An option set with code {code.Value} already exists.");

    var optionSet = OptionSet.Create(
      id: OptionSetId.Of(Guid.NewGuid()),
      code: code,
      label: Name.Of(input.Name, 150),
      description: input.Description,
      isSystem: input.IsSystem);

    if (!input.IsActive)
      optionSet.Update(code, Name.Of(input.Name, 150), input.Description, false);

    foreach (var value in input.Values ?? Array.Empty<OptionSetValueInput>())
    {
      // parents must be created first; values reference each other by id, so initial creation only supports flat lists
      optionSet.AddValue(
        OptionSetValueId.Of(Guid.NewGuid()),
        parentValueId: null,
        LookupCode.Of(value.Value),
        value.Label,
        value.Color,
        value.Icon,
        value.DisplayOrder);
    }

    await context.OptionSets.AddAsync(optionSet, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateOptionSetCommandResult>.Success(new CreateOptionSetCommandResult(optionSet.Id.Value));
  }
}
