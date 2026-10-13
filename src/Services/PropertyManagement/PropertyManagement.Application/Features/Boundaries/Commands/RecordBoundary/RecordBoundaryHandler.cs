using Microsoft.EntityFrameworkCore;

public class RecordBoundaryHandler(IApplicationDbContext context)
  : ICommandHandler<RecordBoundaryCommand, Result<RecordBoundaryCommandResult>>
{
  public async Task<Result<RecordBoundaryCommandResult>> Handle(RecordBoundaryCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Boundary;

    var current = await context.Boundaries.Where(b => b.PropertyId == property.Id && b.IsCurrent).ToListAsync(cancellationToken);

    // the domain decides whether this survey supersedes the current one, and warns on a steep slope
    var boundary = PropertyBoundary.Record(
      BoundaryId.New(), property, input.BoundaryType, input.SurveyDate, input.SurveySource, input.SlopeDegrees,
      input.Remarks, input.Points.ToGeoPoints(), PropertyWriteGuard.Today(), current, out var warnings);

    await context.Boundaries.AddAsync(boundary, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordBoundaryCommandResult>.Success(
      new RecordBoundaryCommandResult(boundary.Id.Value, boundary.Points.Count, boundary.IsCurrent, warnings));
  }
}
