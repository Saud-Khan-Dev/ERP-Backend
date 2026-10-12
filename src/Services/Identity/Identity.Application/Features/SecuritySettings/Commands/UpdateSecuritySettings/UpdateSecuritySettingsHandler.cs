using Microsoft.EntityFrameworkCore;

/// Edits the sign-in security policy. The new values apply to sign-ins, password changes and
/// tokens issued from now on; tokens already issued keep the lifetime they were given.
public class UpdateSecuritySettingsHandler(IApplicationDbContext context,
    IActivityRecorder activity)
  : ICommandHandler<UpdateSecuritySettingsCommand, Result<UpdateSecuritySettingsCommandResult>>
{
  public async Task<Result<UpdateSecuritySettingsCommandResult>> Handle(UpdateSecuritySettingsCommand command, CancellationToken cancellationToken)
  {
    var input = command.Settings;
    var id = SecuritySettingsId.Of(SecuritySettings.SingletonId);

    var settings = await context.SecuritySettings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    if (settings is null)
    {
      settings = SecuritySettings.CreateDefault();
      await context.SecuritySettings.AddAsync(settings, cancellationToken);
    }

    var before = settings.ToDto();

    settings.Update(
      input.PasswordMinimumLength, input.PasswordRequireUppercase, input.PasswordRequireLowercase, input.PasswordRequireDigit, input.PasswordRequireNonAlphanumeric,
      input.MaxFailedLoginAttempts, input.LockoutMinutes, input.AccessTokenMinutes, input.RefreshTokenDays);

    var after = settings.ToDto();
    var detail = ActivityDetail.Changes(
      ("Minimum password length", $"{before.PasswordMinimumLength}", $"{after.PasswordMinimumLength}"),
      ("Upper-case letter required", ActivityDetail.YesNo(before.PasswordRequireUppercase), ActivityDetail.YesNo(after.PasswordRequireUppercase)),
      ("Lower-case letter required", ActivityDetail.YesNo(before.PasswordRequireLowercase), ActivityDetail.YesNo(after.PasswordRequireLowercase)),
      ("Digit required", ActivityDetail.YesNo(before.PasswordRequireDigit), ActivityDetail.YesNo(after.PasswordRequireDigit)),
      ("Symbol required", ActivityDetail.YesNo(before.PasswordRequireNonAlphanumeric), ActivityDetail.YesNo(after.PasswordRequireNonAlphanumeric)),
      ("Wrong attempts before lockout", $"{before.MaxFailedLoginAttempts}", $"{after.MaxFailedLoginAttempts}"),
      ("Locked for (minutes)", $"{before.LockoutMinutes}", $"{after.LockoutMinutes}"),
      ("Access token (minutes)", $"{before.AccessTokenMinutes}", $"{after.AccessTokenMinutes}"),
      ("Signed in for (days)", $"{before.RefreshTokenDays}", $"{after.RefreshTokenDays}"));
    if (detail is not null)
      await activity.RecordAsync(ActivityAction.SecuritySettingsUpdated, ActivityTargetType.System, null, "Security settings", detail, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateSecuritySettingsCommandResult>.Success(new UpdateSecuritySettingsCommandResult(settings.ToDto()));
  }
}
