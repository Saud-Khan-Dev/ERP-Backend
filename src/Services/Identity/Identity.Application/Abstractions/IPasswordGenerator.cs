/// Generates the one-time password a Super Admin hands to a new employee.
/// Generated server-side so provisioning never depends on an administrator inventing a good password.
public interface IPasswordGenerator
{
  string Generate(PasswordPolicyOptions policy);
}
