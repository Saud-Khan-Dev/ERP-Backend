using Microsoft.EntityFrameworkCore;

/// Issues and checks employee codes against the admin-editable <see cref="EmployeeCodeTemplate"/>.
///
/// Nothing is saved here: the template counter moves on the tracked entity and the calling handler
/// commits it together with the user, so a failed create never burns a number. Two concurrent
/// issues collide on the template's row version and the loser gets a 409 instead of a duplicate.
public class EmployeeCodeService(IApplicationDbContext context)
{
  private const int MaxIssueAttempts = 100;

  /// The tracked template, created with the defaults (EMP-###, starting at 1) the first time it is needed.
  public async Task<EmployeeCodeTemplate> GetTemplateAsync(CancellationToken cancellationToken)
  {
    var id = EmployeeCodeTemplateId.Of(EmployeeCodeTemplate.SingletonId);

    var template = await context.EmployeeCodeTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    if (template is null)
    {
      template = EmployeeCodeTemplate.CreateDefault();
      await context.EmployeeCodeTemplates.AddAsync(template, cancellationToken);
    }

    return template;
  }

  /// Code for a new account: the one the administrator typed (it must fit the template), otherwise
  /// the next one from the template, or none when auto-generation is switched off.
  public async Task<Result<EmployeeCode?>> ForNewUserAsync(string? requested, bool autoGenerate, CancellationToken cancellationToken)
  {
    if (!string.IsNullOrWhiteSpace(requested))
      return await ClaimAsync(EmployeeCode.Of(requested), self: null, cancellationToken);

    if (!autoGenerate)
      return Result<EmployeeCode?>.Success(null);

    return Result<EmployeeCode?>.Success(await IssueAsync(cancellationToken));
  }

  /// Code after a profile edit. An unchanged code is kept as-is even when it predates the current
  /// template; only a new value has to fit it. Null or blank clears the code.
  public async Task<Result<EmployeeCode?>> ForExistingUserAsync(User user, string? requested, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    if (string.IsNullOrWhiteSpace(requested))
      return Result<EmployeeCode?>.Success(null);

    var code = EmployeeCode.Of(requested);

    if (code == user.EmployeeCode)
      return Result<EmployeeCode?>.Success(code);

    return await ClaimAsync(code, user.Id, cancellationToken);
  }

  /// A template edit must not make the counter hand out a code that is already on an account.
  /// Returns the failure message, or null when the template is safe to save.
  public async Task<string?> FindTemplateConflictAsync(EmployeeCodeTemplate template, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(template);

    var codes = await context.Users.IgnoreQueryFilters().AsNoTracking()
        .Where(u => u.EmployeeCode != null)
        .Select(u => u.EmployeeCode!)
        .ToListAsync(cancellationToken);

    long? highest = null;
    foreach (var code in codes)
      if (template.TryReadNumber(code, out var number) && (highest is null || number > highest))
        highest = number;

    if (highest is not null && highest >= template.NextNumber)
      return $"{template.Format(highest.Value).Value} is already in use; the next number must be at least {highest + 1}.";

    return null;
  }

  private async Task<Result<EmployeeCode?>> ClaimAsync(EmployeeCode code, UserId? self, CancellationToken cancellationToken)
  {
    var template = await GetTemplateAsync(cancellationToken);
    template.EnsureMatches(code);

    if (await IsTakenAsync(code, self, cancellationToken))
      return Result<EmployeeCode?>.Failure($"Employee code '{code.Value}' is already assigned to another account.");

    // a hand-typed EMP-150 moves the counter past 150 so auto-generation never repeats it
    template.Reserve(code);

    return Result<EmployeeCode?>.Success(code);
  }

  private async Task<EmployeeCode> IssueAsync(CancellationToken cancellationToken)
  {
    var template = await GetTemplateAsync(cancellationToken);

    // codes can also be typed in by hand or left over from an earlier template, so skip any in use
    for (var attempt = 0; attempt < MaxIssueAttempts; attempt++)
    {
      var code = template.Issue();

      if (!await IsTakenAsync(code, self: null, cancellationToken))
        return code;
    }

    throw new DomainException(
      $"Could not find a free employee code after {MaxIssueAttempts} attempts. Raise the template's next number.");
  }

  private Task<bool> IsTakenAsync(EmployeeCode code, UserId? self, CancellationToken cancellationToken)
  {
    // soft-deleted accounts keep their code so audit history stays unambiguous
    var users = context.Users.IgnoreQueryFilters();

    if (self is not null)
      users = users.Where(u => u.Id != self);

    return users.AnyAsync(u => u.EmployeeCode == code, cancellationToken);
  }
}
