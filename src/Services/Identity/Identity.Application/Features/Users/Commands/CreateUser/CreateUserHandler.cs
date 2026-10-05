using Microsoft.EntityFrameworkCore;

/// The Super Admin provisioning workflow, in one transaction:
/// create the account → link the employee and give it an employee code → assign roles → hand back a one-time password.
///
/// There is deliberately no public counterpart to this handler: an employee cannot create an account.
public class CreateUserHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    IdentityGuard guard,
    EmployeeCodeService employeeCodes,
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

    if (input.EmployeeId is { } employeeId && employeeId != Guid.Empty
        && await context.Users.IgnoreQueryFilters().AnyAsync(u => u.EmployeeId == employeeId, cancellationToken))
      return Result<CreateUserCommandResult>.Failure("That employee already has a login account.");

    var employeeCode = await employeeCodes.ForNewUserAsync(input.EmployeeCode, input.AutoGenerateEmployeeCode, cancellationToken);
    if (!employeeCode.IsSuccess)
      return Result<CreateUserCommandResult>.Failure(employeeCode.Message!);

    // roles are resolved and authorized before anything is written
    var roles = await LoadRolesAsync(input.RoleIds, cancellationToken);
    foreach (var role in roles)
      guard.EnsureCanAdministerRole(role);

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
      employeeCode: employeeCode.Value,
      // a generated password must always be changed on first use
      mustChangePassword: input.MustChangePassword || generated,
      now: now);

    await context.Users.AddAsync(user, cancellationToken);

    foreach (var role in roles)
      await context.UserRoles.AddAsync(
        UserRole.Create(user.Id, role.Id, currentUser.UserId, expiresAt: null, now), cancellationToken);

    await activity.RecordAsync(ActivityAction.UserCreated, ActivityTargetType.User, user.Id.Value, user.Username.Value,
      roles.Count == 0 ? null : string.Join(", ", roles.Select(r => r.RoleName.Value)), cancellationToken);

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
      throw new DomainException($"Role '{inactive.Code.Value}' is inactive and cannot be assigned.");

    return roles;
  }
}
