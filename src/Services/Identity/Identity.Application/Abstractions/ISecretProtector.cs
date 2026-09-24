/// Encrypts secrets that must be stored but never disclosed — currently MFA seeds.
/// A plaintext TOTP secret in the database is as good as the password itself.
public interface ISecretProtector
{
  string Protect(string plaintext);

  string Unprotect(string protectedValue);
}
