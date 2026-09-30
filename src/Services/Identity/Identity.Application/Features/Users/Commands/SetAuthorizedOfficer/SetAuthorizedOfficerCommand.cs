using FluentValidation;

public sealed record SetAuthorizedOfficerCommandResult(bool IsAuthorizedOfficer);

/// Designates (or un-designates) an officer authorized by the DG (GDA Act s.2(a-i)). Only such an officer
/// may impose fines (s.28) or file complaints in court (s.30). Takes effect with the user's next access
/// token, i.e. within the token lifetime.
public sealed record SetAuthorizedOfficerCommand(Guid Id, bool IsAuthorizedOfficer) : ICommand<Result<SetAuthorizedOfficerCommandResult>>;

public class SetAuthorizedOfficerCommandValidator : AbstractValidator<SetAuthorizedOfficerCommand>
{
  public SetAuthorizedOfficerCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
