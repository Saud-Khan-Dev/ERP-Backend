using FluentValidation;

public sealed record LitigationDetailsInput(
  string CaseTitle,
  Guid LitigationTypeId,
  DateOnly? FilingDate = null,
  GdaRole? GdaRole = null,
  Guid? FiledByOfficerId = null,
  string? GdaCounsel = null,
  string? Remarks = null);

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
    RuleFor(x => x.CaseTitle).NotEmpty().MaximumLength(300);
    RuleFor(x => x.LitigationTypeId).NotEmpty();
    RuleFor(x => x.GdaRole).IsInEnum().When(x => x.GdaRole.HasValue);
    RuleFor(x => x.GdaCounsel).MaximumLength(150);
  }
}

public class LitigationPartyInputValidator : AbstractValidator<LitigationPartyInput>
{
  public LitigationPartyInputValidator()
  {
    RuleFor(x => x).Must(p => !string.IsNullOrWhiteSpace(p.PartyName) || p.PartyOwnerId.HasValue)
      .WithMessage("A party needs a name or a registered owner.");
    RuleFor(x => x.PartyName).MaximumLength(200);
    RuleFor(x => x.PartyRole).IsInEnum();
    RuleFor(x => x.CounselName).MaximumLength(150);
  }
}

public sealed record FileLitigationCommandResult(Guid Id);

/// Records a court / committee case. FiledByOfficerId defaults to the signed-in user — for a criminal
/// complaint that must be the officer authorized by the DG (Act s.30). With ParentLitigationId the case is
/// an appeal to a higher court, and the lower-court case is marked APPEALED.
public sealed record FileLitigationCommand(Guid PropertyId, LitigationInput Litigation) : ICommand<Result<FileLitigationCommandResult>>;

public class FileLitigationCommandValidator : AbstractValidator<FileLitigationCommand>
{
  public FileLitigationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Litigation).NotNull();
    RuleFor(x => x.Litigation.CaseNo).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Litigation.CourtAuthority).NotEmpty().MaximumLength(200);
    RuleFor(x => x.Litigation.Details).NotNull().SetValidator(new LitigationDetailsInputValidator());
    RuleForEach(x => x.Litigation.Parties).SetValidator(new LitigationPartyInputValidator());
  }
}

public static class LitigationInputs
{
  public static async Task<PropertyLitigation.Details> ToDetailsAsync(this LitigationDetailsInput input, MasterLookup masters, ICurrentUser currentUser, CancellationToken cancellationToken)
  {
    var type = await masters.GetAsync<LitigationType>(input.LitigationTypeId, cancellationToken);
    var filedBy = input.FiledByOfficerId ?? currentUser.UserId;

    // Act s.30: only an officer authorized by the DG files a complaint in court. The designation travels
    // in the officer's own token, so the complaint is recorded against the signed-in authorized officer.
    if (type.Is(SystemMasterCodes.LitigationCriminalComplaint))
    {
      if (!currentUser.IsAuthorizedOfficer || currentUser.UserId is null)
        throw new AuthorizedOfficerRequiredException("Only an officer authorized by the DG may file a complaint in court (GDA Act s.30).");

      if (filedBy != currentUser.UserId)
        throw new DomainException("A complaint is recorded against the authorized officer filing it: leave filedByOfficerId empty or give your own id.");
    }

    return new PropertyLitigation.Details(input.CaseTitle, type, input.FilingDate, input.GdaRole, filedBy, input.GdaCounsel, input.Remarks);
  }

  public static async Task<PropertyLitigation.PartyInput> ToPartyAsync(this LitigationPartyInput input, IApplicationDbContext context, CancellationToken cancellationToken)
  {
    var owner = input.PartyOwnerId is { } ownerId ? await context.LoadOwnerAsync(ownerId, cancellationToken) : null;
    return new PropertyLitigation.PartyInput(input.PartyName ?? string.Empty, owner, input.PartyRole, input.CounselName, input.Remarks);
  }
}
