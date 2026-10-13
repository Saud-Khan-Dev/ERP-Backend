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

    var details = await input.Details.ToDetailsAsync(masters, cancellationToken);

    // Act s.30: a criminal complaint is filed by the signed-in authorized officer (403 for anyone else)
    Guid? filingOfficer = details.LitigationType.Is(SystemMasterCodes.LitigationCriminalComplaint) ? currentUser.RequireAuthorizedOfficer() : null;

    var parent = input.ParentLitigationId is { } p ? await context.LoadLitigationAsync(p, cancellationToken) : null;
    var parentStatus = parent is null ? null : await masters.GetAsync<LitigationStatus>(parent.LitigationStatusId.Value, cancellationToken);

    var related = new PropertyLitigation.Related(
      input.RelatedEncroachmentId is { } e ? await context.LoadEncroachmentAsync(e, cancellationToken) : null,
      input.RelatedAllotmentId is { } a ? await context.LoadAllotmentAsync(a, cancellationToken) : null,
      input.RelatedLeaseId is { } l ? await context.LoadLeaseAsync(l, cancellationToken) : null,
      parent, parentStatus);

    var parties = new List<PropertyLitigation.PartyInput>();
    foreach (var party in input.Parties ?? Array.Empty<LitigationPartyInput>())
      parties.Add(await party.ToPartyAsync(context, cancellationToken));

    var litigation = PropertyLitigation.File(LitigationId.New(), property, caseNo, court, status, details, related, parties, filingOfficer);

    // an appeal to a higher court: the (decided) lower-court case is now APPEALED
    if (parent is not null)
      parent.MarkAppealed(parentStatus!, await masters.GetByCodeAsync<LitigationStatus>(SystemMasterCodes.Appealed, cancellationToken), court);

    // the encroachment the case is about is now under litigation (unless already resolved)
    related.Encroachment?.MarkUnderLitigation(await masters.GetByCodeAsync<EncroachmentStatus>(SystemMasterCodes.UnderLitigation, cancellationToken));

    await context.Litigations.AddAsync(litigation, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<FileLitigationCommandResult>.Success(new FileLitigationCommandResult(litigation.Id.Value));
  }
}
