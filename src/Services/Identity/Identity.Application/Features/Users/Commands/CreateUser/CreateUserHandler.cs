using Microsoft.EntityFrameworkCore;

/// The Super Admin provisioning workflow, in one transaction:
/// create the account → link it to the person's HR record (taking HR's employee number) → assign roles → hand back a
/// one-time password, which the person must change at first sign-in.
///
/// There is deliberately no public counterpart to this handler: an employee cannot create an account.
public class CreateUserHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    IdentityGuard guard,
    ICurrentUser currentUser,
    ISecuritySettingsProvider securitySettings,
    IActivityRecorder activity)
  : ICommandHandler<CreateUserCommand, Result<CreateUserCommandResult>>
{
  public async Task<Result<CreateUserCommandResult>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var input = command.User;
    var policy = (await securitySettings.GetAsync(cancellationToken)).PasswordPolicy;

    var username = Username.Of(input.Username);
    var email = EmailAddress.Of(input.Email);

    if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, cancellationToken))
      return Result<CreateUserCommandResult>.Failure($"Username '{username.Value}' is already taken.");

    if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
      return Result<CreateUserCommandResult>.Failure($"Email '{email.Value}' is already registered.");

    if (input.EmployeeId is { } employeeId && employeeId != Guid.Empty)
    {
      var holder = await context.Users.IgnoreQueryFilters().AsNoTracking()
        .Where(u => u.EmployeeId == employeeId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
      if (holder is not null)
        return Result<CreateUserCommandResult>.Failure($"This person already has an account ({holder}).");
    }
    var employeeCode = input.EmployeeId is null || string.IsNullOrWhiteSpace(input.EmployeeCode) ? null : EmployeeCode.Of(input.EmployeeCode);

    // roles are resolved and authorized before anything is written
    var roles = await LoadRolesAsync(input.RoleIds, cancellationToken);
    foreach (var role in roles)
      guard.EnsureCanAdministerRole(role);
    if (roles.Count > 0)
    {
      // giving roles is the role-giving right, not the account-creating one - and nobody gives what they do not hold
      guard.EnsurePermission(PermissionCatalog.Users.Assign, "give roles to a new account (that needs the right to give roles)");
      await guard.EnsureCanGrantRolesAsync(roles.Select(r => r.Id), "give these roles", cancellationToken);
    }
    var roleIds = roles.Where(r => !r.IsSuperAdmin).Select(r => r.Id).ToList();
    var roleCodes = roleIds.Count == 0 || roles.Any(r => r.IsSuperAdmin) ? [] : await context.RolePermissions.AsNoTracking()
      .Where(rp => roleIds.Contains(rp.RoleId))
      .Join(context.Permissions.AsNoTracking().Where(p => p.IsActive), rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code.Value)
      .Distinct().ToListAsync(cancellationToken);

    var generated = input.TemporaryPassword is null;
    var password = input.TemporaryPassword ?? passwordGenerator.Generate(policy);
    PasswordPolicy.Validate(password, policy);

    var user = User.Create(
      id: UserId.Of(Guid.NewGuid()),
      username: username,
      email: email,
      displayName: Name.Of(input.DisplayName),
      passwordHash: PasswordHash.Of(passwordHasher.Hash(password)),
      employeeId: input.EmployeeId,
      employeeCode: employeeCode,
      // whoever typed or generated it, the administrator knows the first password - the person always changes it
      mustChangePassword: true,
      now: now);

    await context.Users.AddAsync(user, cancellationToken);

    foreach (var role in roles)
      await context.UserRoles.AddAsync(
        UserRole.Create(user.Id, role.Id, currentUser.UserId, expiresAt: null, now), cancellationToken);

    await activity.RecordAsync(ActivityAction.UserCreated, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value,
      roles.Count == 0 ? null : string.Join(", ", roles.Select(r => r.RoleName.Value)), cancellationToken);
    await ConflictCheck.EnsureAcceptedAsync(activity, roleCodes, input.ConflictReason, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateUserCommandResult>.Success(
      new CreateUserCommandResult(user.Id.Value, generated ? password : null, user.EmployeeCode?.Value));
  }

  private async Task<List<Role>> LoadRolesAsync(IReadOnlyList<Guid>? roleIds, CancellationToken cancellationToken)
  {
    if (roleIds is null || roleIds.Count == 0)
      return new List<Role>();

    var ids = roleIds.Distinct().Select(RoleId.Of).ToList();

    var roles = await context.Roles.Where(r => ids.Contains(r.Id)).ToListAsync(cancellationToken);

    if (roles.Count != ids.Count)
      throw new RoleNotFoundException("One or more of the supplied roles was not found.");

    var inactive = roles.FirstOrDefault(r => !r.IsActive);
    if (inactive is not null)
      throw new DomainException($"The role {inactive.RoleName.Value} is switched off and cannot be given.");

    return roles;
  }
}
