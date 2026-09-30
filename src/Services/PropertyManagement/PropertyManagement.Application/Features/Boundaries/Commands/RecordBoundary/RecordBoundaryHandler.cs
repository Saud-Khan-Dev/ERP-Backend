using Microsoft.EntityFrameworkCore;

public class RecordBoundaryHandler(IApplicationDbContext context)
  : ICommandHandler<RecordBoundaryCommand, Result<RecordBoundaryCommandResult>>
{
  public async Task<Result<RecordBoundaryCommandResult>> Handle(RecordBoundaryCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Boundary;

    var boundary = PropertyBoundary.Record(
      BoundaryId.New(), property, input.BoundaryType, input.SurveyDate, input.SurveySource, input.SlopePercentage,
      input.Remarks, input.Points.ToGeoPoints());

    var previous = await context.Boundaries.Where(b => b.PropertyId == property.Id && b.IsCurrent).ToListAsync(cancellationToken);
    previous.ForEach(b => b.Supersede());

    await context.Boundaries.AddAsync(boundary, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordBoundaryCommandResult>.Success(new RecordBoundaryCommandResult(boundary.Id.Value, boundary.Points.Count));
  }
}
