/// Rules a plaintext password must satisfy before it is hashed.
public sealed record PasswordPolicyOptions(
    int MinimumLength = 8,
    bool RequireUppercase = true,
    bool RequireLowercase = true,
    bool RequireDigit = true,
    bool RequireNonAlphanumeric = false)
{
  public const int MaximumLength = 256;
}

public static class PasswordPolicy
{
  /// Throws a DomainException listing everything that is wrong, so the caller does not have to
  /// discover the rules one failed attempt at a time.
  public static void Validate(string? password, PasswordPolicyOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (string.IsNullOrWhiteSpace(password))
      throw new DomainException("Password is required.");

    var problems = new List<string>();

    if (password.Length < options.MinimumLength)
      problems.Add($"be at least {options.MinimumLength} characters");

    if (password.Length > PasswordPolicyOptions.MaximumLength)
      problems.Add($"be at most {PasswordPolicyOptions.MaximumLength} characters");

    if (options.RequireUppercase && !password.Any(char.IsUpper))
      problems.Add("contain an upper-case letter");

    if (options.RequireLowercase && !password.Any(char.IsLower))
      problems.Add("contain a lower-case letter");

    if (options.RequireDigit && !password.Any(char.IsDigit))
      problems.Add("contain a digit");

    if (options.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
      problems.Add("contain a symbol");

    if (problems.Count > 0)
      throw new DomainException($"Password must {string.Join(", ", problems)}.");
  }
}
