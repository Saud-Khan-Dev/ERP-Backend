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

    settings.Update(
      input.PasswordMinimumLength, input.PasswordRequireUppercase, input.PasswordRequireLowercase, input.PasswordRequireDigit, input.PasswordRequireNonAlphanumeric,
      input.MaxFailedLoginAttempts, input.LockoutMinutes, input.AccessTokenMinutes, input.RefreshTokenDays);

    await activity.RecordAsync(ActivityAction.SecuritySettingsUpdated, ActivityTargetType.System, null, "Security settings", null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateSecuritySettingsCommandResult>.Success(new UpdateSecuritySettingsCommandResult(settings.ToDto()));
  }
}
