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
  public static async Task<PropertyLitigation.Details> ToDetailsAsync(this LitigationDetailsInput input, MasterLookup masters, ICurrentUser currentUser, CancellationToken cancellationToken) => new(
    input.CaseTitle,
    await masters.GetAsync<LitigationType>(input.LitigationTypeId, cancellationToken),
    input.FilingDate, input.GdaRole, input.FiledByOfficerId ?? currentUser.UserId, input.GdaCounsel, input.Remarks);

  public static async Task<PropertyLitigation.PartyInput> ToPartyAsync(this LitigationPartyInput input, IApplicationDbContext context, CancellationToken cancellationToken)
  {
    var owner = input.PartyOwnerId is { } ownerId ? await context.LoadOwnerAsync(ownerId, cancellationToken) : null;
    return new PropertyLitigation.PartyInput(input.PartyName ?? string.Empty, owner, input.PartyRole, input.CounselName, input.Remarks);
  }
}
