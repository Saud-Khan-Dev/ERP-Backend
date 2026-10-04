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

/// SlopePercentage: ground slope of the surveyed plot, e.g. 12.50 for a 12.50% slope (not degrees).
public sealed record BoundaryInput(
  IReadOnlyList<GeoPointInput> Points,
  BoundaryType BoundaryType = BoundaryType.Original,
  DateOnly? SurveyDate = null,
  string? SurveySource = null,
  decimal? SlopePercentage = null,
  string? Remarks = null);

public sealed record RecordBoundaryCommandResult(Guid Id, int PointCount);

/// A new survey of the plot. The previous current boundary is kept but stops being current.
public sealed record RecordBoundaryCommand(Guid PropertyId, BoundaryInput Boundary) : ICommand<Result<RecordBoundaryCommandResult>>;

public class BoundaryInputValidator : AbstractValidator<BoundaryInput>
{
  public BoundaryInputValidator()
  {
    RuleFor(x => x.BoundaryType).IsInEnum();
    RuleFor(x => x.Points).NotNull().Must(p => p is { Count: >= 3 }).WithMessage("A boundary needs at least three GPS points.");
    RuleForEach(x => x.Points).SetValidator(new GeoPointInputValidator());
    RuleFor(x => x.SlopePercentage).InclusiveBetween(0, PropertyBoundary.MaxSlopePercentage)
      .When(x => x.SlopePercentage.HasValue)
      .WithMessage("Slope is a percentage from 0 upwards, e.g. 12.50 for a 12.50% slope.");
    RuleFor(x => x.SurveySource).MaximumLength(150);
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
