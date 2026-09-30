using Microsoft.EntityFrameworkCore;

public class FileLitigationHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<FileLitigationCommand, Result<FileLitigationCommandResult>>
{
  public async Task<Result<FileLitigationCommandResult>> Handle(FileLitigationCommand command, CancellationToken cancellationToken)
  {
    var input = command.Litigation;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);

    var caseNo = input.CaseNo.Trim();
    var court = input.CourtAuthority.Trim();
    if (await context.Litigations.AnyAsync(l => l.CaseNo == caseNo && l.CourtAuthority == court, cancellationToken))
      return Result<FileLitigationCommandResult>.Failure($"Case {caseNo} before {court} is already recorded.");

    var status = input.LitigationStatusId is { } statusId
      ? await masters.GetAsync<LitigationStatus>(statusId, cancellationToken)
      : await masters.GetByCodeAsync<LitigationStatus>(SystemMasterCodes.Pending, cancellationToken);

    var related = new PropertyLitigation.Related(
      input.RelatedEncroachmentId is { } e ? await context.LoadEncroachmentAsync(e, cancellationToken) : null,
      input.RelatedAllotmentId is { } a ? await context.LoadAllotmentAsync(a, cancellationToken) : null,
      input.RelatedLeaseId is { } l ? await context.LoadLeaseAsync(l, cancellationToken) : null,
      input.ParentLitigationId is { } p ? await context.LoadLitigationAsync(p, cancellationToken) : null);

    var parties = new List<PropertyLitigation.PartyInput>();
    foreach (var party in input.Parties ?? Array.Empty<LitigationPartyInput>())
      parties.Add(await party.ToPartyAsync(context, cancellationToken));

    var litigation = PropertyLitigation.File(
      LitigationId.New(), property, caseNo, court, status,
      await input.Details.ToDetailsAsync(masters, currentUser, cancellationToken), related, parties);

    related.Parent?.MarkAppealed(await masters.GetByCodeAsync<LitigationStatus>(SystemMasterCodes.Appealed, cancellationToken), court);

    await context.Litigations.AddAsync(litigation, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<FileLitigationCommandResult>.Success(new FileLitigationCommandResult(litigation.Id.Value));
  }
}
