public class EncroachmentActionsHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<UpdateEncroachmentCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<IssueEncroachmentNoticeCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<ChangeEncroachmentStatusCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<ResolveEncroachmentCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<SetEncroachmentBoundaryCommand, Result<EncroachmentActionResult>>
{
  public async Task<Result<EncroachmentActionResult>> Handle(UpdateEncroachmentCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(encroachment.PropertyId, cancellationToken);
    encroachment.Update(await command.Details.ToDetailsAsync(context, cancellationToken));
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(IssueEncroachmentNoticeCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(encroachment.PropertyId, cancellationToken);
    encroachment.IssueNotice(
      await masters.GetAsync<EncroachmentStatus>(encroachment.EncroachmentStatusId.Value, cancellationToken),
      await masters.GetByCodeAsync<EncroachmentStatus>(SystemMasterCodes.UnderNotice, cancellationToken),
      command.NoticeNo, command.NoticeDate, PropertyWriteGuard.Today());
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(ChangeEncroachmentStatusCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(encroachment.PropertyId, cancellationToken);
    encroachment.ChangeStatus(await masters.GetAsync<EncroachmentStatus>(command.EncroachmentStatusId, cancellationToken));
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(ResolveEncroachmentCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(encroachment.PropertyId, cancellationToken);
    var regularized = command.ResolutionType == EncroachmentResolution.Regularized;

    var today = PropertyWriteGuard.Today();
    encroachment.Resolve(
      await masters.GetByCodeAsync<EncroachmentStatus>(regularized ? SystemMasterCodes.Regularized : SystemMasterCodes.Resolved, cancellationToken),
      command.ResolutionDate, command.ResolutionType, command.ResolutionReferenceNo, today);
    audit.Record(PropertyAuditActions.Resolved, encroachment, string.Join(" · ", new[] { AuditProfile.Words(command.ResolutionType.ToString()), command.ResolutionReferenceNo }.Where(x => !string.IsNullOrWhiteSpace(x))));

    PropertyAreaRegularization? regularization = null;
    if (command.OpenRegularization)
    {
      // schema note: a regularized encroachment may also produce a property_area_regularization row; resolving is an
      // approver's act (the route), so the case is recorded as decided by the signed-in officer
      var property = await context.LoadPropertyAsync(encroachment.PropertyId.Value, cancellationToken);
      regularization = PropertyAreaRegularization.Open(
        AreaRegularizationId.New(), property,
        await masters.GetAsync<MeasurementUnit>(encroachment.MeasurementUnitId.Value, cancellationToken),
        encroachment.EncroachmentArea, encroachment.DetectionDate,
        new PropertyAreaRegularization.Decision(RegularizationStatus.Regularized, command.ResolutionDate, command.ResolutionReferenceNo,
          currentUser.AuditName, currentUser.EmployeeId, command.ResolutionDate, null,
          $"Regularized encroachment {encroachment.EncroachmentNo.Value}."),
        today);

      await context.AreaRegularizations.AddAsync(regularization, cancellationToken);
    }

    return await SaveAsync(encroachment, regularization?.Id, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(SetEncroachmentBoundaryCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(encroachment.PropertyId, cancellationToken);
    encroachment.SetBoundary(command.Points.ToGeoPoints());
    audit.Record(AuditActions.Changed, encroachment, field: "Encroached area boundary", newValue: $"{command.Points.Count} GPS points");
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  private async Task<Result<EncroachmentActionResult>> SaveAsync(PropertyEncroachment encroachment, AreaRegularizationId? regularizationId, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<EncroachmentStatus>(encroachment.EncroachmentStatusId).LoadAsync(cancellationToken);
    return Result<EncroachmentActionResult>.Success(
      new EncroachmentActionResult(encroachment.Id.Value, refs[encroachment.EncroachmentStatusId], regularizationId?.Value));
  }
}
