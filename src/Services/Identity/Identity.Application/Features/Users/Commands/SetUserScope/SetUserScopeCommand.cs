using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// A town or office as Administration names it (the id is the town's in Property, the office's in HRM).
public sealed record ScopePlaceDto(Guid Id, string Label);

/// Where a person works: the towns (Property and Taxation) and the offices (HRM, sub-units included). An empty list means
/// every town / every office.
public sealed record UserScopeDto(IReadOnlyList<ScopePlaceDto> Towns, IReadOnlyList<ScopePlaceDto> Offices);

public sealed record SetUserScopeCommand(Guid UserId, IReadOnlyList<ScopePlaceDto> Towns, IReadOnlyList<ScopePlaceDto> Offices)
  : ICommand<Result<UserScopeDto>>;

public class SetUserScopeValidator : AbstractValidator<SetUserScopeCommand>
{
  public SetUserScopeValidator()
  {
    RuleFor(x => x.Towns).Must(t => t.Count <= 100).WithMessage("Choose at most 100 towns.");
    RuleFor(x => x.Offices).Must(o => o.Count <= 100).WithMessage("Choose at most 100 offices.");
    RuleForEach(x => x.Towns).Must(p => p.Id != Guid.Empty && !string.IsNullOrWhiteSpace(p.Label)).WithMessage("Every town needs its id and name.");
    RuleForEach(x => x.Offices).Must(p => p.Id != Guid.Empty && !string.IsNullOrWhiteSpace(p.Label)).WithMessage("Every office needs its id and name.");
  }
}

/// Sets where a person works - one act, one activity line, the person's tokens re-issued. The guard rails of any access
/// change: not your own account, not an account that can do more than you.
public class SetUserScopeHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity)
  : ICommandHandler<SetUserScopeCommand, Result<UserScopeDto>>
{
  public async Task<Result<UserScopeDto>> Handle(SetUserScopeCommand command, CancellationToken cancellationToken)
  {
    var userId = UserId.Of(command.UserId);
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found - it may have been deleted.");
    guard.EnsureNotSelf(userId, "set the towns and offices of");
    await guard.EnsureCanActOnAsync(userId, "set the towns and offices of", cancellationToken);

    var existing = await context.UserScopes.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
    string Words(IEnumerable<string> labels) => labels.Any() ? string.Join(", ", labels.OrderBy(l => l)) : "All";
    var beforeTowns = Words(existing.Where(s => s.Kind == ScopeKind.Town).Select(s => s.Label));
    var beforeOffices = Words(existing.Where(s => s.Kind == ScopeKind.Office).Select(s => s.Label));

    context.UserScopes.RemoveRange(existing);
    var towns = command.Towns.DistinctBy(t => t.Id).ToList();
    var offices = command.Offices.DistinctBy(o => o.Id).ToList();
    foreach (var town in towns)
      await context.UserScopes.AddAsync(UserScope.Create(userId, ScopeKind.Town, town.Id, town.Label), cancellationToken);
    foreach (var office in offices)
      await context.UserScopes.AddAsync(UserScope.Create(userId, ScopeKind.Office, office.Id, office.Label), cancellationToken);

    var detail = ActivityDetail.Changes(
      ("Towns", beforeTowns, Words(towns.Select(t => t.Label.Trim()))),
      ("Offices", beforeOffices, Words(offices.Select(o => o.Label.Trim()))));
    if (detail is null)
      return Result<UserScopeDto>.Success(new UserScopeDto(towns, offices));

    await activity.RecordAsync(ActivityAction.ScopeChanged, ActivityTargetType.User, userId.Value, user.Username.Value, detail, cancellationToken);
    user.AccessChanged();
    await context.SaveChangesAsync(cancellationToken);
    return Result<UserScopeDto>.Success(new UserScopeDto(towns, offices));
  }
}
