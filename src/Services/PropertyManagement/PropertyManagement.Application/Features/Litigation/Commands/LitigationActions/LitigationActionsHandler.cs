public class LitigationActionsHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<UpdateLitigationCommand, Result<LitigationActionResult>>,
    ICommandHandler<AddLitigationPartyCommand, Result<LitigationActionResult>>,
    ICommandHandler<RecordHearingCommand, Result<LitigationActionResult>>,
    ICommandHandler<ChangeLitigationStatusCommand, Result<LitigationActionResult>>,
    ICommandHandler<DecideLitigationCommand, Result<LitigationActionResult>>
{
  public async Task<Result<LitigationActionResult>> Handle(UpdateLitigationCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(litigation.PropertyId, cancellationToken);
    var details = await command.Details.ToDetailsAsync(masters, cancellationToken);

    // only an edit that turns the case into a criminal complaint needs the signed-in authorized officer (s.30);
    // the officer who filed an existing complaint stays on record
    Guid? officer = litigation.BecomesCriminalComplaint(details.LitigationType) ? currentUser.RequireAuthorizedOfficer() : null;

    litigation.Update(await Current(litigation, cancellationToken), details, officer);
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(AddLitigationPartyCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(litigation.PropertyId, cancellationToken);
    litigation.AddParty(await Current(litigation, cancellationToken), await command.Party.ToPartyAsync(context, cancellationToken));
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(RecordHearingCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(litigation.PropertyId, cancellationToken);
    litigation.RecordHearing(await Current(litigation, cancellationToken), command.HearingDate, command.Proceedings,
      command.OrderPassed, command.NextHearingDate, command.AttendedBy, command.AttendedByEmployeeId, PropertyWriteGuard.Today());
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(ChangeLitigationStatusCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(litigation.PropertyId, cancellationToken);
    litigation.ChangeStatus(await Current(litigation, cancellationToken), await masters.GetAsync<LitigationStatus>(command.LitigationStatusId, cancellationToken));
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(DecideLitigationCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(litigation.PropertyId, cancellationToken);
    var today = PropertyWriteGuard.Today();
    litigation.Decide(await Current(litigation, cancellationToken),
      await masters.GetByCodeAsync<LitigationStatus>(SystemMasterCodes.Decided, cancellationToken), command.DecisionDate, command.DecisionOutcome,
      command.DecisionResult, today);
    audit.Record(PropertyAuditActions.Decided, litigation, string.Join(" · ", new[] { command.DecisionResult is { } result ? AuditProfile.Words(result.ToString()) : null, command.DecisionOutcome }.Where(x => !string.IsNullOrWhiteSpace(x))));

    var effects = new List<string>();
    if (command.ResolveRelatedEncroachment && litigation.RelatedEncroachmentId is { } encroachmentId)
    {
      var encroachment = await context.LoadEncroachmentAsync(encroachmentId.Value, cancellationToken);
      if (encroachment.IsUnresolved)
      {
        encroachment.Resolve(await masters.GetByCodeAsync<EncroachmentStatus>(SystemMasterCodes.Resolved, cancellationToken),
          command.DecisionDate < encroachment.DetectionDate ? encroachment.DetectionDate : command.DecisionDate,
          EncroachmentResolution.Litigated, litigation.CaseNo, today);
        effects.Add($"Encroachment {encroachment.EncroachmentNo.Value} is resolved by this decision.");
      }
      else
        effects.Add($"Encroachment {encroachment.EncroachmentNo.Value} was already resolved.");
    }

    return await SaveAsync(litigation, cancellationToken, effects);
  }

  private Task<LitigationStatus> Current(PropertyLitigation litigation, CancellationToken cancellationToken) =>
      masters.GetAsync<LitigationStatus>(litigation.LitigationStatusId.Value, cancellationToken);

  private async Task<Result<LitigationActionResult>> SaveAsync(PropertyLitigation litigation, CancellationToken cancellationToken, IReadOnlyList<string>? effects = null)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<LitigationStatus>(litigation.LitigationStatusId).LoadAsync(cancellationToken);
    return Result<LitigationActionResult>.Success(new LitigationActionResult(litigation.Id.Value, refs[litigation.LitigationStatusId], litigation.NextHearingDate,
      effects ?? Array.Empty<string>()));
  }
}
