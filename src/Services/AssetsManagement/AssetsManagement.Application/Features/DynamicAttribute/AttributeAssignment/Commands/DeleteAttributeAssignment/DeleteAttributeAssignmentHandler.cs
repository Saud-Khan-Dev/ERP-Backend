using Microsoft.EntityFrameworkCore;

public class DeleteAttributeAssignmentHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : ICommandHandler<DeleteAttributeAssignmentCommand, Result<DeleteAttributeAssignmentCommandResult>>
{
  public async Task<Result<DeleteAttributeAssignmentCommandResult>> Handle(DeleteAttributeAssignmentCommand command, CancellationToken cancellationToken)
  {
    var id = AttributeAssignmentId.Of(command.Id);
    var assignment = await context.AttributeAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
      ?? throw new AttributeAssignmentNotFoundException("That field is no longer on this form.");

    // the field leaves the form here; the values assets hold for it stay on their records (shown as "no longer on
    // the form") - nothing is erased
    context.AttributeAssignments.Remove(assignment);
    await context.SaveChangesAsync(cancellationToken);

    var assets = await AssignmentReach.AssetsUnderAsync(context, assignment, cancellationToken);
    await schemaService.ReprojectAsync(assets, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteAttributeAssignmentCommandResult>.Success(new DeleteAttributeAssignmentCommandResult(true));
  }
}
