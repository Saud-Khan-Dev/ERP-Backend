using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

/// Fills in the actor (from the access token) and the request's IP and device, then adds the line
/// to the context. The caller's SaveChanges persists it alongside the change it describes.
///
/// Sentences read with people's names: the actor is written as their display name, and an account the act was done to
/// is labelled with its display name too ("Farukh Jadoon gave Ayesha Noor the role Tax clerk").
public sealed class ActivityRecorder(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : IActivityRecorder
{
  public async Task RecordAsync(
      ActivityAction action,
      ActivityTargetType targetType,
      Guid? targetId,
      string? targetLabel,
      string? detail,
      CancellationToken cancellationToken)
  {
    if (currentUser.UserId is not { } actorId)
      return;

    var http = httpContextAccessor.HttpContext;
    var ip = IpAddress.OfNullable(http?.Connection.RemoteIpAddress?.ToString());
    var userAgent = http?.Request.Headers.UserAgent.ToString();

    var actor = UserId.Of(actorId);
    var actorName = await NameOfAsync(actor, cancellationToken) ?? currentUser.Username ?? currentUser.AuditName;
    if (targetType == ActivityTargetType.User && targetId is { } target)
      targetLabel = await NameOfAsync(UserId.Of(target), cancellationToken) ?? targetLabel;

    var activity = AdminActivity.Record(
      actor,
      actorName,
      action, targetType, targetId, targetLabel, Fit(detail),
      ip, userAgent, DateTime.UtcNow);

    await context.AdminActivities.AddAsync(activity, cancellationToken);
  }

  // the account as it is in this unit of work (a new account is not saved yet), deleted or not
  private async Task<string?> NameOfAsync(UserId id, CancellationToken cancellationToken) =>
    context.Users.Local.FirstOrDefault(u => u.Id == id)?.DisplayName.Value
      ?? await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == id)
        .Select(u => u.DisplayName.Value).FirstOrDefaultAsync(cancellationToken);

  // the column holds 500 characters; a longer detail must never fail the change it describes
  private static string? Fit(string? detail) =>
    detail is { Length: > DetailMaxLength } ? detail[..(DetailMaxLength - 1)] + "\u2026" : detail;

  private const int DetailMaxLength = 500;
}
