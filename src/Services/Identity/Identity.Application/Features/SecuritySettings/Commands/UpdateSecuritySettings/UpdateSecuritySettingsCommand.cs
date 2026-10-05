using FluentValidation;

public sealed record SecuritySettingsInput(
  int PasswordMinimumLength,
  bool PasswordRequireUppercase,
  bool PasswordRequireLowercase,
  bool PasswordRequireDigit,
  bool PasswordRequireNonAlphanumeric,
  int MaxFailedLoginAttempts,
  int LockoutMinutes,
  int AccessTokenMinutes,
  int RefreshTokenDays);

public sealed record UpdateSecuritySettingsCommandResult(SecuritySettingsDto Settings);

public sealed record UpdateSecuritySettingsCommand(SecuritySettingsInput Settings)
  : ICommand<Result<UpdateSecuritySettingsCommandResult>>;

public class SecuritySettingsInputValidator : AbstractValidator<SecuritySettingsInput>
{
  public SecuritySettingsInputValidator()
  {
    RuleFor(x => x.PasswordMinimumLength).InclusiveBetween(SecuritySettings.MinPasswordLength, SecuritySettings.MaxPasswordLength);
    RuleFor(x => x.MaxFailedLoginAttempts).InclusiveBetween(SecuritySettings.MinFailedAttempts, SecuritySettings.MaxFailedAttempts);
    RuleFor(x => x.LockoutMinutes).InclusiveBetween(SecuritySettings.MinLockoutMinutes, SecuritySettings.MaxLockoutMinutes);
    RuleFor(x => x.AccessTokenMinutes).InclusiveBetween(SecuritySettings.MinAccessTokenMinutes, SecuritySettings.MaxAccessTokenMinutes);
    RuleFor(x => x.RefreshTokenDays).InclusiveBetween(SecuritySettings.MinRefreshTokenDays, SecuritySettings.MaxRefreshTokenDays);
  }
}

public class UpdateSecuritySettingsCommandValidator : AbstractValidator<UpdateSecuritySettingsCommand>
{
  public UpdateSecuritySettingsCommandValidator()
  {
    RuleFor(x => x.Settings).NotNull().SetValidator(new SecuritySettingsInputValidator());
  }
}
