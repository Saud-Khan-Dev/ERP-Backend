using FluentValidation;

/// The filing officer of a criminal complaint is not part of the input: it is the signed-in authorized
/// officer (Act s.30), recorded when the complaint is filed and never changed by edits.
/// GdaCounsel / GdaCounselEmployeeId: GDA's counsel - picked from the staff list (the HR record and the name as it stood
/// then), or a name only for an advocate who is not GDA staff.
public sealed record LitigationDetailsInput(
  string CaseTitle,
  Guid LitigationTypeId,
  DateOnly? FilingDate = null,
  GdaRole? GdaRole = null,
  string? GdaCounsel = null,
  string? Remarks = null,
  Guid? GdaCounselEmployeeId = null);

public sealed record LitigationPartyInput(string? PartyName, LitigationPartyRole PartyRole, Guid? PartyOwnerId = null, string? CounselName = null, string? Remarks = null);

public sealed record LitigationInput(
  string CaseNo,
  string CourtAuthority,
  LitigationDetailsInput Details,
  Guid? LitigationStatusId = null,
  Guid? RelatedEncroachmentId = null,
  Guid? RelatedAllotmentId = null,
  Guid? RelatedLeaseId = null,
  Guid? ParentLitigationId = null,
  IReadOnlyList<LitigationPartyInput>? Parties = null);

public class LitigationDetailsInputValidator : AbstractValidator<LitigationDetailsInput>
{
  public LitigationDetailsInputValidator()
  {
    RuleFor(x => x.CaseTitle).NotEmpty().MaximumLength(PropertyLitigation.CaseTitleMaxLength);
    RuleFor(x => x.LitigationTypeId).NotEmpty();
    RuleFor(x => x.GdaRole).IsInEnum().When(x => x.GdaRole.HasValue);
    RuleFor(x => x.GdaCounsel).MaximumLength(PropertyLitigation.CounselMaxLength);
    RuleFor(x => x.Remarks).MaximumLength(PropertyLitigation.TextMaxLength);
  }
}

public class LitigationPartyInputValidator : AbstractValidator<LitigationPartyInput>
{
  public LitigationPartyInputValidator()
  {
    RuleFor(x => x).Must(p => !string.IsNullOrWhiteSpace(p.PartyName) || p.PartyOwnerId.HasValue)
      .WithMessage("A party needs a name or a registered owner.");
    RuleFor(x => x.PartyName).MaximumLength(LitigationParty.NameMaxLength);
    RuleFor(x => x.PartyRole).IsInEnum();
    RuleFor(x => x.CounselName).MaximumLength(LitigationParty.CounselMaxLength);
    RuleFor(x => x.Remarks).MaximumLength(LitigationParty.RemarksMaxLength);
  }
}

public sealed record FileLitigationCommandResult(Guid Id);

/// Records a court / committee case; it starts open (pending by default). A criminal complaint (Act s.30) is
/// filed by the signed-in authorized officer, who is recorded as its filing officer (anyone else gets 403).
/// With RelatedEncroachmentId an unresolved encroachment moves to UNDER_LITIGATION. With ParentLitigationId
/// the case is an appeal to a higher court: the lower-court case must be decided, and it is marked APPEALED.
public sealed record FileLitigationCommand(Guid PropertyId, LitigationInput Litigation) : ICommand<Result<FileLitigationCommandResult>>;

public class FileLitigationCommandValidator : AbstractValidator<FileLitigationCommand>
{
  public FileLitigationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Litigation).NotNull();
    RuleFor(x => x.Litigation.CaseNo).NotEmpty().MaximumLength(PropertyLitigation.CaseNoMaxLength);
    RuleFor(x => x.Litigation.CourtAuthority).NotEmpty().MaximumLength(PropertyLitigation.CourtAuthorityMaxLength);
    RuleFor(x => x.Litigation.Details).NotNull().SetValidator(new LitigationDetailsInputValidator());
    RuleForEach(x => x.Litigation.Parties).SetValidator(new LitigationPartyInputValidator());
  }
}

public static class LitigationInputs
{
  public static async Task<PropertyLitigation.Details> ToDetailsAsync(this LitigationDetailsInput input, MasterLookup masters, CancellationToken cancellationToken) => new(
    input.CaseTitle, await masters.GetAsync<LitigationType>(input.LitigationTypeId, cancellationToken), input.FilingDate, input.GdaRole,
    input.GdaCounsel, input.Remarks, input.GdaCounselEmployeeId);

  /// Act s.30: only an officer authorized by the DG files a complaint in court. The designation travels in the
  /// officer's own token, so the complaint is recorded against the signed-in authorized officer.
  public static Guid RequireAuthorizedOfficer(this ICurrentUser currentUser) =>
      currentUser.IsAuthorizedOfficer && currentUser.UserId is { } userId
        ? userId
        : throw new AuthorizedOfficerRequiredException("Only an officer authorized by the DG may file a complaint in court (GDA Act s.30).");

  public static async Task<PropertyLitigation.PartyInput> ToPartyAsync(this LitigationPartyInput input, IApplicationDbContext context, CancellationToken cancellationToken)
  {
    var owner = input.PartyOwnerId is { } ownerId ? await context.LoadOwnerAsync(ownerId, cancellationToken) : null;
    return new PropertyLitigation.PartyInput(input.PartyName ?? string.Empty, owner, input.PartyRole, input.CounselName, input.Remarks);
  }
}
