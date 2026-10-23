public class ViolationActionsHandler(IApplicationDbContext context, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<IssueViolationNoticeCommand, Result<ViolationActionResult>>,
    ICommandHandler<ImposeFineCommand, Result<ViolationActionResult>>,
    ICommandHandler<SetFineStatusCommand, Result<ViolationActionResult>>,
    ICommandHandler<RectifyViolationCommand, Result<ViolationActionResult>>
{
  public async Task<Result<ViolationActionResult>> Handle(IssueViolationNoticeCommand command, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(violation.PropertyId, cancellationToken);
    var warnings = violation.IssueNotice(new AgreementViolation.Notice(command.NoticeNo, command.NoticeDate, command.NoticeDeadline), PropertyWriteGuard.Today());
    return await SaveAsync(violation, cancellationToken, warnings);
  }

  public async Task<Result<ViolationActionResult>> Handle(ImposeFineCommand command, CancellationToken cancellationToken)
  {
    // s.28-A: the fine is imposed by an officer authorized by the DG (app_user.is_authorized_officer)
    if (!currentUser.IsAuthorizedOfficer || currentUser.UserId is null)
      throw new AuthorizedOfficerRequiredException("Only an officer authorized by the DG may impose a fine (GDA Act s.28-A).");

    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(violation.PropertyId, cancellationToken);
    violation.ImposeFine(command.FineAmount, currentUser.UserId.Value, PropertyWriteGuard.Today());
    audit.Record(PropertyAuditActions.Imposed, violation, $"Fine of Rs {command.FineAmount:N0} (GDA Act s.28-A)");
    return await SaveAsync(violation, cancellationToken);
  }

  public async Task<Result<ViolationActionResult>> Handle(SetFineStatusCommand command, CancellationToken cancellationToken)
  {
    // a payment is a record (with its receipt, PROPERTY.EDIT); waiving a fine or sending it for recovery as land revenue is a
    // decision of its own (PROPERTY.WAIVE). The route lets either code in; the act decides which one it needs.
    if (command.FineStatus is FineStatus.Waived or FineStatus.RecoveryAsArrears && !currentUser.HasPermission(PermissionCatalog.Property.Waive))
      throw new PermissionRequiredException(command.FineStatus == FineStatus.Waived
        ? "Waiving a fine needs Property: waive fines (PROPERTY.WAIVE); ask an officer who holds it."
        : "Sending a fine for recovery as arrears of land revenue needs Property: waive fines (PROPERTY.WAIVE); ask an officer who holds it.");
    if (command.FineStatus is not (FineStatus.Waived or FineStatus.RecoveryAsArrears) && !currentUser.HasPermission(PermissionCatalog.Property.Edit))
      throw new PermissionRequiredException("Recording a fine as paid needs Property: edit (PROPERTY.EDIT).");

    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(violation.PropertyId, cancellationToken);
    violation.ChangeFineStatus(new AgreementViolation.FineChange(command.FineStatus, command.ReceiptNo, command.PaidOn, command.Reason),
      PropertyWriteGuard.Today(), currentUser.UserId);
    if (command.FineStatus is FineStatus.Waived)
      audit.Record(PropertyAuditActions.Waived, violation, command.Reason);
    else if (command.FineStatus is FineStatus.RecoveryAsArrears)
      audit.Record(PropertyAuditActions.SentForRecovery, violation, "Fine to be recovered as arrears of land revenue (s.28(2))");
    return await SaveAsync(violation, cancellationToken);
  }

  public async Task<Result<ViolationActionResult>> Handle(RectifyViolationCommand command, CancellationToken cancellationToken)
  {
    var violation = await context.LoadViolationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(violation.PropertyId, cancellationToken);
    violation.Rectify(command.Remarks);
    return await SaveAsync(violation, cancellationToken);
  }

  private async Task<Result<ViolationActionResult>> SaveAsync(AgreementViolation violation, CancellationToken cancellationToken, IReadOnlyList<string>? warnings = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    return Result<ViolationActionResult>.Success(
      new ViolationActionResult(violation.Id.Value, violation.ViolationStatus, violation.FineStatus, warnings ?? Array.Empty<string>()));
  }
}
