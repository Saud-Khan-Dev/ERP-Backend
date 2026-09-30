public class LitigationActionsHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<UpdateLitigationCommand, Result<LitigationActionResult>>,
    ICommandHandler<AddLitigationPartyCommand, Result<LitigationActionResult>>,
    ICommandHandler<RecordHearingCommand, Result<LitigationActionResult>>,
    ICommandHandler<ChangeLitigationStatusCommand, Result<LitigationActionResult>>,
    ICommandHandler<DecideLitigationCommand, Result<LitigationActionResult>>
{
  public async Task<Result<LitigationActionResult>> Handle(UpdateLitigationCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    litigation.Update(await Current(litigation, cancellationToken), await command.Details.ToDetailsAsync(masters, currentUser, cancellationToken));
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(AddLitigationPartyCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    litigation.AddParty(await command.Party.ToPartyAsync(context, cancellationToken));
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(RecordHearingCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    litigation.RecordHearing(await Current(litigation, cancellationToken), command.HearingDate, command.Proceedings,
      command.OrderPassed, command.NextHearingDate, command.AttendedBy);
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(ChangeLitigationStatusCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    litigation.ChangeStatus(await Current(litigation, cancellationToken), await masters.GetAsync<LitigationStatus>(command.LitigationStatusId, cancellationToken));
    return await SaveAsync(litigation, cancellationToken);
  }

  public async Task<Result<LitigationActionResult>> Handle(DecideLitigationCommand command, CancellationToken cancellationToken)
  {
    var litigation = await context.LoadLitigationAsync(command.Id, cancellationToken);
    litigation.Decide(await Current(litigation, cancellationToken),
      await masters.GetByCodeAsync<LitigationStatus>(SystemMasterCodes.Decided, cancellationToken), command.DecisionDate, command.DecisionOutcome);
    return await SaveAsync(litigation, cancellationToken);
  }

  private Task<LitigationStatus> Current(PropertyLitigation litigation, CancellationToken cancellationToken) =>
      masters.GetAsync<LitigationStatus>(litigation.LitigationStatusId.Value, cancellationToken);

  private async Task<Result<LitigationActionResult>> SaveAsync(PropertyLitigation litigation, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    var refs = await masters.Refs().Add<LitigationStatus>(litigation.LitigationStatusId).LoadAsync(cancellationToken);
    return Result<LitigationActionResult>.Success(new LitigationActionResult(litigation.Id.Value, refs[litigation.LitigationStatusId], litigation.NextHearingDate));
  }
}
