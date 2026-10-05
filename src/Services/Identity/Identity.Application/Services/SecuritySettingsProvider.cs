using Microsoft.EntityFrameworkCore;

/// Reads the one security-settings row. Until the seeder has written it, the built-in defaults
/// stand in, so the service is never left without a policy.
public sealed class SecuritySettingsProvider(IApplicationDbContext context) : ISecuritySettingsProvider
{
  public async Task<SecuritySettings> GetAsync(CancellationToken cancellationToken)
  {
    var id = SecuritySettingsId.Of(SecuritySettings.SingletonId);

    return await context.SecuritySettings.AsNoTracking()
        .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
      ?? SecuritySettings.CreateDefault();
  }
}
