using FluentValidation;

public sealed record GetLoginAttemptsQueryResult(PaginatedResult<LoginAttemptDto> Attempts);

/// Security audit: who tried to sign in, from where, and whether it worked.
/// `Reason` narrows failures: wrong-password, unknown-username, locked, deactivated, deleted.
public sealed record GetLoginAttemptsQuery(
  PaginationRequest Pagination,
  Guid? UserId = null,
  string? Username = null,
  bool? Succeeded = null,
  DateTime? From = null,
  DateTime? To = null,
  string? Reason = null) : IQuery<Result<GetLoginAttemptsQueryResult>>;

/// The reasons a sign-in fails, as the Sign-ins screen filters them, and the recorded texts behind each.
public static class SignInFailureReasons
{
  public static readonly IReadOnlyDictionary<string, string[]> Texts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
  {
    ["wrong-password"] = ["Wrong password", "Wrong password — account locked"],
    ["unknown-username"] = ["Unknown username"],
    ["locked"] = ["Account locked", "Wrong password — account locked"],
    // "Account unavailable" is what older lines say for any of the three
    ["deactivated"] = ["Account deactivated", "Account unavailable"],
    ["deleted"] = ["Account deleted"],
  };
}

public class GetLoginAttemptsQueryValidator : AbstractValidator<GetLoginAttemptsQuery>
{
  public GetLoginAttemptsQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0).WithMessage("The page number cannot be negative.");
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 200).WithMessage("Ask for between 1 and 200 sign-ins at a time.");
    RuleFor(x => x.Reason).Must(r => string.IsNullOrWhiteSpace(r) || SignInFailureReasons.Texts.ContainsKey(r))
      .WithMessage($"The reason is one of: {string.Join(", ", SignInFailureReasons.Texts.Keys)}.");
  }
}
