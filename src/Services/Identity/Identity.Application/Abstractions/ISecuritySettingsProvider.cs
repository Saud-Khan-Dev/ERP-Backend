/// Supplies the current security policy (password rules, lockout, session lifetimes) to the
/// handlers that enforce it. One place reads the admin-editable settings, so sign-in, password
/// changes and token issuance all honour the same values without each knowing where they live.
public interface ISecuritySettingsProvider
{
  Task<SecuritySettings> GetAsync(CancellationToken cancellationToken);
}
