using Microsoft.EntityFrameworkCore;

public class UpdateOptionSetHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateOptionSetCommand, Result<UpdateOptionSetCommandResult>>
{
  public async Task<Result<UpdateOptionSetCommandResult>> Handle(UpdateOptionSetCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.Id);
    var optionSet = await context.OptionSets.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.Id} was not found.");

    var code = LookupCode.Of(command.Code);

    if (await context.OptionSets.AnyAsync(o => o.Id != id && o.Code == code, cancellationToken))
      return Result<UpdateOptionSetCommandResult>.Failure($"An option set with code {code.Value} already exists.");

    optionSet.Update(code, Name.Of(command.Name, 150), command.Description, command.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateOptionSetCommandResult>.Success(new UpdateOptionSetCommandResult(true));
  }
}
