public class DeactivateRegularizationHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<DeactivateRegularizationCommand, Result<DeactivateRegularizationCommandResult>>
{
  public async Task<Result<DeactivateRegularizationCommandResult>> Handle(DeactivateRegularizationCommand command, CancellationToken cancellationToken)
  {
    var regularization = await context.LoadRegularizationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(regularization.PropertyId, cancellationToken);

    // withdrawing a regularized case takes its area off the property: it undoes an approver's decision
    if (regularization.RegularizationStatus == RegularizationStatus.Regularized && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException("This case was regularized; withdrawing it takes its area off the property, which is an approver's act.");

    regularization.Deactivate();

    await context.SaveChangesAsync(cancellationToken);
    return Result<DeactivateRegularizationCommandResult>.Success(new DeactivateRegularizationCommandResult(true));
  }
}
