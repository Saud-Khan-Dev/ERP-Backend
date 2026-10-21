/// The password rules in force (the security settings), for every password form's live checklist: sign-in pages need
/// them before anyone is signed in, so the route is anonymous - the rules are not a secret.
public sealed record PasswordPolicyDto(
  int MinimumLength,
  int MaximumLength,
  bool RequireUppercase,
  bool RequireLowercase,
  bool RequireDigit,
  bool RequireNonAlphanumeric);

public sealed record GetPasswordPolicyQuery : IQuery<Result<PasswordPolicyDto>>;

public class GetPasswordPolicyHandler(ISecuritySettingsProvider settings) : IQueryHandler<GetPasswordPolicyQuery, Result<PasswordPolicyDto>>
{
  public async Task<Result<PasswordPolicyDto>> Handle(GetPasswordPolicyQuery query, CancellationToken cancellationToken)
  {
    var policy = (await settings.GetAsync(cancellationToken)).PasswordPolicy;
    return Result<PasswordPolicyDto>.Success(new PasswordPolicyDto(
      policy.MinimumLength, PasswordPolicyOptions.MaximumLength,
      policy.RequireUppercase, policy.RequireLowercase, policy.RequireDigit, policy.RequireNonAlphanumeric));
  }
}
