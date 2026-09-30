public class EncroachmentActionsHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateEncroachmentCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<IssueEncroachmentNoticeCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<ChangeEncroachmentStatusCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<ResolveEncroachmentCommand, Result<EncroachmentActionResult>>,
    ICommandHandler<SetEncroachmentBoundaryCommand, Result<EncroachmentActionResult>>
{
  public async Task<Result<EncroachmentActionResult>> Handle(UpdateEncroachmentCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    encroachment.Update(await command.Details.ToDetailsAsync(context, cancellationToken));
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(IssueEncroachmentNoticeCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    encroachment.IssueNotice(command.NoticeNo, command.NoticeDate);
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(ChangeEncroachmentStatusCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    encroachment.ChangeStatus(await masters.GetAsync<EncroachmentStatus>(command.EncroachmentStatusId, cancellationToken));
    return await SaveAsync(encroachment, null, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(ResolveEncroachmentCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    var regularized = command.ResolutionType == EncroachmentResolution.Regularized;

    encroachment.Resolve(
      await masters.GetByCodeAsync<EncroachmentStatus>(regularized ? SystemMasterCodes.Regularized : SystemMasterCodes.Resolved, cancellationToken),
      command.ResolutionDate, command.ResolutionType, command.ResolutionReferenceNo);

    PropertyAreaRegularization? regularization = null;
    if (command.OpenRegularization)
    {
      // schema note: a regularized encroachment may also produce a property_area_regularization row
      var property = await context.LoadPropertyAsync(encroachment.PropertyId.Value, cancellationToken);
      regularization = PropertyAreaRegularization.Open(
        AreaRegularizationId.New(), property,
        await masters.GetAsync<MeasurementUnit>(encroachment.MeasurementUnitId.Value, cancellationToken),
        encroachment.EncroachmentArea, RegularizationStatus.Regularized, encroachment.DetectionDate, command.ResolutionDate,
        command.ResolutionReferenceNo, approvedBy: null, command.ResolutionDate,
        $"Regularized encroachment {encroachment.EncroachmentNo.Value}.");

      await context.AreaRegularizations.AddAsync(regularization, cancellationToken);
    }

    return await SaveAsync(encroachment, regularization?.Id, cancellationToken);
  }

  public async Task<Result<EncroachmentActionResult>> Handle(SetEncroachmentBoundaryCommand command, CancellationToken cancellationToken)
  {
    var encroachment = await context.LoadEncroachmentAsync(command.Id, cancellationToken);
    encroachment.SetBoundary(command.Points.ToGeoPoints());
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
