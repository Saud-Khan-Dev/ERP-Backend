using Microsoft.EntityFrameworkCore;

public class DeleteAttributeAssignmentHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteAttributeAssignmentCommand, Result<DeleteAttributeAssignmentCommandResult>>
{
  public async Task<Result<DeleteAttributeAssignmentCommandResult>> Handle(DeleteAttributeAssignmentCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeAssignmentId.Of(command.Id);
    var assignment = await context.AttributeAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AttributeAssignmentNotFoundException($"Attribute assignment {command.Id} was not found.");

    // Existing asset values stay in extra_attributes (history is kept); they simply stop being validated / shown.
    context.AttributeAssignments.Remove(assignment);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAttributeAssignmentCommandResult>.Success(new DeleteAttributeAssignmentCommandResult(true));
  }
}
