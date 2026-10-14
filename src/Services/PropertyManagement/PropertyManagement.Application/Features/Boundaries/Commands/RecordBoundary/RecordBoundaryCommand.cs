using FluentValidation;

/// One GPS corner: latitude / longitude only (no elevation).
public sealed record GeoPointInput(decimal Latitude, decimal Longitude);

public class GeoPointInputValidator : AbstractValidator<GeoPointInput>
{
  public GeoPointInputValidator()
  {
    RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
    RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
  }
}

public static class GeoPointInputs
{
  public static IReadOnlyList<GeoPoint> ToGeoPoints(this IEnumerable<GeoPointInput>? points) =>
      (points ?? Array.Empty<GeoPointInput>()).Select(p => GeoPoint.Of(p.Latitude, p.Longitude)).ToList();
}

/// SlopeDegrees: ground slope of the surveyed plot in degrees, 0 to 90 (above 45 the survey is recorded with
/// a Building & Construction Regulations 2026 warning).
public sealed record BoundaryInput(
  IReadOnlyList<GeoPointInput> Points,
  BoundaryType BoundaryType = BoundaryType.Original,
  DateOnly? SurveyDate = null,
  string? SurveySource = null,
  decimal? SlopeDegrees = null,
  string? Remarks = null);

/// IsCurrent: false when the survey is older than the boundary in force (kept as history).
/// Warnings: slope above 45° (BCR 2026), or the survey kept as history. Never a refusal.
public sealed record RecordBoundaryCommandResult(Guid Id, int PointCount, bool IsCurrent, IReadOnlyList<string> Warnings);

/// A new survey of the plot. When it is the latest dated one, the previous current boundary is kept but
/// stops being current; an older survey is stored as history.
public sealed record RecordBoundaryCommand(Guid PropertyId, BoundaryInput Boundary) : ICommand<Result<RecordBoundaryCommandResult>>;

public class BoundaryInputValidator : AbstractValidator<BoundaryInput>
{
  public BoundaryInputValidator()
  {
    RuleFor(x => x.BoundaryType).IsInEnum();
    RuleFor(x => x.Points).NotNull().Must(p => p is { Count: >= 3 }).WithMessage("A boundary needs at least three GPS points.");
    RuleForEach(x => x.Points).SetValidator(new GeoPointInputValidator());
    RuleFor(x => x.SlopeDegrees).InclusiveBetween(0, PropertyBoundary.MaxSlopeDegrees)
      .When(x => x.SlopeDegrees.HasValue)
      .WithMessage(PropertyBoundary.SlopeRangeMessage);
    RuleFor(x => x.SurveyDate).LessThanOrEqualTo(_ => PropertyWriteGuard.Today()).When(x => x.SurveyDate.HasValue)
      .WithMessage("The survey date cannot be in the future.");
    RuleFor(x => x.SurveySource).MaximumLength(PropertyBoundary.SurveySourceMaxLength);
    RuleFor(x => x.Remarks).MaximumLength(PropertyBoundary.RemarksMaxLength);
  }
}

public class RecordBoundaryCommandValidator : AbstractValidator<RecordBoundaryCommand>
{
  public RecordBoundaryCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Boundary).NotNull().SetValidator(new BoundaryInputValidator());
  }
}
