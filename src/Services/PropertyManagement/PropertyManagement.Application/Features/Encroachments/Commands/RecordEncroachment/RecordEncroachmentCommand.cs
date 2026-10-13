using FluentValidation;

public sealed record EncroachmentDetailsInput(
  string? EncroacherName = null,
  Guid? EncroacherOwnerId = null,
  DateOnly? EffectiveDate = null,
  string? Description = null,
  string? Remarks = null);

public sealed record EncroachmentInput(
  decimal EncroachmentArea,
  Guid MeasurementUnitId,
  DateOnly DetectionDate,
  EncroachmentDetailsInput? Details = null,
  Guid? EncroachmentStatusId = null,
  string? NoticeNo = null,
  DateOnly? NoticeDate = null,
  IReadOnlyList<GeoPointInput>? Points = null);

public sealed record RecordEncroachmentCommandResult(Guid Id, string EncroachmentNo);

/// ENC-00001 is generated; status defaults to ACTIVE. Points (optional) are the encroached polygon.
public sealed record RecordEncroachmentCommand(Guid PropertyId, EncroachmentInput Encroachment) : ICommand<Result<RecordEncroachmentCommandResult>>;

public class RecordEncroachmentCommandValidator : AbstractValidator<RecordEncroachmentCommand>
{
  public RecordEncroachmentCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Encroachment).NotNull();
    RuleFor(x => x.Encroachment.EncroachmentArea).GreaterThan(0);
    RuleFor(x => x.Encroachment.MeasurementUnitId).NotEmpty();
    RuleFor(x => x.Encroachment.DetectionDate).LessThanOrEqualTo(_ => PropertyWriteGuard.Today())
      .WithMessage("The detection date cannot be in the future.");
    RuleFor(x => x.Encroachment.NoticeNo).MaximumLength(PropertyEncroachment.NoticeNoMaxLength);
    RuleFor(x => x.Encroachment.NoticeNo).NotEmpty().When(x => x.Encroachment.NoticeDate.HasValue)
      .WithMessage("A notice date needs its notice no. Enter the notice no., or clear the notice date.");
    RuleFor(x => x.Encroachment.Details!).SetValidator(new EncroachmentDetailsInputValidator()).When(x => x.Encroachment.Details is not null);
    RuleFor(x => x.Encroachment.Details!.EffectiveDate).LessThanOrEqualTo(x => x.Encroachment.DetectionDate)
      .When(x => x.Encroachment.Details?.EffectiveDate is not null)
      .WithMessage("In place since cannot be after the detection date.");
    RuleForEach(x => x.Encroachment.Points).SetValidator(new GeoPointInputValidator());
  }
}

public class EncroachmentDetailsInputValidator : AbstractValidator<EncroachmentDetailsInput>
{
  public EncroachmentDetailsInputValidator()
  {
    RuleFor(x => x.EncroacherName).MaximumLength(PropertyEncroachment.EncroacherNameMaxLength);
    RuleFor(x => x.Description).MaximumLength(PropertyEncroachment.TextMaxLength);
    RuleFor(x => x.Remarks).MaximumLength(PropertyEncroachment.TextMaxLength);
  }
}

public static class EncroachmentInputs
{
  public static async Task<PropertyEncroachment.Details> ToDetailsAsync(this EncroachmentDetailsInput? input, IApplicationDbContext context, CancellationToken cancellationToken)
  {
    input ??= new EncroachmentDetailsInput();
    var encroacher = input.EncroacherOwnerId is { } ownerId ? await context.LoadOwnerAsync(ownerId, cancellationToken) : null;
    return new PropertyEncroachment.Details(input.EncroacherName, encroacher, input.EffectiveDate, input.Description, input.Remarks);
  }
}
