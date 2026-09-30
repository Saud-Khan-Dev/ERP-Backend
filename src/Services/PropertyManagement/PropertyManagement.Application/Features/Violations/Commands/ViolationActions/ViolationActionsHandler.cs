public class ViolationActionsHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<IssueViolationNoticeCommand, Result<ViolationActionResult>>,
    ICommandHandler<ImposeFineCommand, Result<ViolationActionResult>>,
    ICommandHandler<SetFineStatusCommand, Result<ViolationActionResult>>,
    ICommandHandler<RectifyViolationCommand, Result<ViolationActionResult>>
{
  public async Task<Result<ViolationActionResult>> Handle(IssueViolationNoticeCommand command, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    violation.IssueNotice(new AgreementViolation.Notice(command.NoticeNo, command.NoticeDate, command.NoticeDeadline));
    return await SaveAsync(violation, cancellationToken);
  }

  public async Task<Result<ViolationActionResult>> Handle(ImposeFineCommand command, CancellationToken cancellationToken)
  {
    // s.28-A: the fine is imposed by an officer authorized by the DG (app_user.is_authorized_officer)
    if (!currentUser.IsAuthorizedOfficer || currentUser.UserId is null)
      throw new AuthorizedOfficerRequiredException("Only an officer authorized by the DG may impose a fine (GDA Act s.28-A).");

    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    violation.ImposeFine(command.FineAmount, currentUser.UserId.Value);
    return await SaveAsync(violation, cancellationToken);
  }

  public async Task<Result<ViolationActionResult>> Handle(SetFineStatusCommand command, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    violation.SetFineStatus(command.FineStatus);
    return await SaveAsync(violation, cancellationToken);
  }

  public async Task<Result<ViolationActionResult>> Handle(RectifyViolationCommand command, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    violation.Rectify(command.Remarks);
    return await SaveAsync(violation, cancellationToken);
  }

  private async Task<Result<ViolationActionResult>> SaveAsync(AgreementViolation violation, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    return Result<ViolationActionResult>.Success(new ViolationActionResult(violation.Id.Value, violation.ViolationStatus, violation.FineStatus));
  }
}
