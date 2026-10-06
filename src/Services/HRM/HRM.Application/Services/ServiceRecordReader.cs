using Microsoft.EntityFrameworkCore;

/// An employee's regular post on a date: the assignment, the post and the post's version that day.
public sealed record CurrentPost(PositionAssignment Assignment, Post Post, PostVersion Version)
{
  public ServiceChange AsOld() => new(Post.Id, null, Version.GradeId, null, Version.OrgUnitId, null);
}

/// Reads who holds what (position assignments), the questions every service-record use case asks.
public class ServiceRecordReader(IApplicationDbContext context)
{
  /// The employee's live regular assignment covering the date, with its post (tracked).
  public async Task<CurrentPost?> RegularPostOnAsync(EmployeeId employeeId, DateOnly date, CancellationToken cancellationToken)
  {
    var assignment = await context.PositionAssignments
      .Where(a => a.EmployeeId == employeeId && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
        && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .FirstOrDefaultAsync(cancellationToken);

    if (assignment is null)
      return null;

    var post = await context.Posts.Include(p => p.Versions).FirstAsync(p => p.Id == assignment.PostId, cancellationToken);
    var version = post.VersionOn(date)
      ?? throw new DomainException($"Post {post.PostCode} has no version on {date:yyyy-MM-dd}.");

    return new CurrentPost(assignment, post, version);
  }

  /// The employee's live assignments (tracked), for the "one regular post at a time" rule.
  public Task<List<PositionAssignment>> LiveAssignmentsOfEmployeeAsync(EmployeeId employeeId, CancellationToken cancellationToken) =>
      context.PositionAssignments.Where(a => a.EmployeeId == employeeId && a.Status == RecordStatus.Active).ToListAsync(cancellationToken);

  /// The post's live assignments (tracked), for the seat count.
  public Task<List<PositionAssignment>> LiveAssignmentsOfPostAsync(PostId postId, CancellationToken cancellationToken) =>
      context.PositionAssignments.Where(a => a.PostId == postId && a.Status == RecordStatus.Active).ToListAsync(cancellationToken);

  public async Task<int> FilledSeatsOnAsync(PostId postId, DateOnly date, CancellationToken cancellationToken) =>
      await context.PositionAssignments.CountAsync(a => a.PostId == postId && a.AssignmentType == AssignmentType.Regular
        && a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date), cancellationToken);
}
