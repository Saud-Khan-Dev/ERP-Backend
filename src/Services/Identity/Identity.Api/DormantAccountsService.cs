using Microsoft.EntityFrameworkCore;

/// Twice a day: when the oversight settings say so, switches off every account nobody has used for DormantAfterDays
/// (never a Super Administrator - the system must keep someone able to manage it). One activity line names them all,
/// as the system's own act.
public sealed class DormantAccountsService(IServiceScopeFactory scopes, ILogger<DormantAccountsService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // let migrations and the seeder finish first
    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await RunOnceAsync(stoppingToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.LogError(ex, "The dormant-account check failed.");
      }
      await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
    }
  }

  private async Task RunOnceAsync(CancellationToken cancellationToken)
  {
    await using var scope = scopes.CreateAsyncScope();
    var settings = await scope.ServiceProvider.GetRequiredService<ISecuritySettingsProvider>().GetAsync(cancellationToken);
    if (!settings.SwitchOffDormant)
      return;
    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    var activity = scope.ServiceProvider.GetRequiredService<IActivityRecorder>();
    var now = DateTime.UtcNow;
    var before = now.AddDays(-settings.DormantAfterDays);
    var map = await AccessMap.BuildAsync(context, cancellationToken);
    var dormantIds = map.Accounts.Values
      .Where(a => a.User.IsActive && !a.User.IsDeleted && !map.IsSuperAdmin(a) && (a.User.LastLoginAt ?? a.User.CreatedAt ?? now) < before)
      .Select(a => a.User.Id)
      .ToList();
    if (dormantIds.Count == 0)
      return;

    var users = await context.Users.Where(u => dormantIds.Contains(u.Id)).ToListAsync(cancellationToken);
    foreach (var user in users)
      user.Deactivate();
    foreach (var session in await context.Sessions.Where(s => dormantIds.Contains(s.UserId) && s.RevokedAt == null).ToListAsync(cancellationToken))
      session.Revoke(now, "Switched off: dormant");
    await activity.RecordAsAsync(null, "Dormant-account check", ActivityAction.DormantAccountsSwitchedOff, ActivityTargetType.System, null,
      $"{users.Count} account{(users.Count == 1 ? "" : "s")}", $"Unused for {settings.DormantAfterDays} days: {string.Join(", ", users.Select(u => u.DisplayName.Value))}",
      cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    logger.LogInformation("Switched off {Count} dormant account(s).", users.Count);
  }
}
