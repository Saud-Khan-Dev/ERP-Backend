using FluentValidation;

public sealed record EncroachmentActionResult(Guid Id, MasterRef? Status, Guid? RegularizationId = null);

public sealed record UpdateEncroachmentCommand(Guid Id, EncroachmentDetailsInput Details) : ICommand<Result<EncroachmentActionResult>>;

public sealed record IssueEncroachmentNoticeCommand(Guid Id, string NoticeNo, DateOnly NoticeDate) : ICommand<Result<EncroachmentActionResult>>;

/// Under Notice, Under Litigation ...
public sealed record ChangeEncroachmentStatusCommand(Guid Id, Guid EncroachmentStatusId) : ICommand<Result<EncroachmentActionResult>>;

/// Removed / Litigated / Other → RESOLVED; Regularized → REGULARIZED, and with OpenRegularization the
/// encroached area is also recorded as a regularized area case.
public sealed record ResolveEncroachmentCommand(
  Guid Id,
  DateOnly ResolutionDate,
  EncroachmentResolution ResolutionType,
  string? ResolutionReferenceNo = null,
  bool OpenRegularization = false) : ICommand<Result<EncroachmentActionResult>>;

/// Captures the encroached polygon once (it is never overwritten).
public sealed record SetEncroachmentBoundaryCommand(Guid Id, IReadOnlyList<GeoPointInput> Points) : ICommand<Result<EncroachmentActionResult>>;

public class IssueEncroachmentNoticeCommandValidator : AbstractValidator<IssueEncroachmentNoticeCommand>
{
  public IssueEncroachmentNoticeCommandValidator() => RuleFor(x => x.NoticeNo).NotEmpty().MaximumLength(50);
}

public class ResolveEncroachmentCommandValidator : AbstractValidator<ResolveEncroachmentCommand>
{
  public ResolveEncroachmentCommandValidator()
  {
    RuleFor(x => x.ResolutionType).IsInEnum();
    RuleFor(x => x.ResolutionReferenceNo).MaximumLength(100);
    RuleFor(x => x.OpenRegularization).Equal(false).When(x => x.ResolutionType != EncroachmentResolution.Regularized)
      .WithMessage("Only a regularized encroachment can open an area regularization case.");
  }
}

public class SetEncroachmentBoundaryCommandValidator : AbstractValidator<SetEncroachmentBoundaryCommand>
{
  public SetEncroachmentBoundaryCommandValidator()
  {
    RuleFor(x => x.Points).NotNull().Must(p => p.Count >= 3).WithMessage("A boundary needs at least three GPS points.");
    RuleForEach(x => x.Points).SetValidator(new GeoPointInputValidator());
  }
}
