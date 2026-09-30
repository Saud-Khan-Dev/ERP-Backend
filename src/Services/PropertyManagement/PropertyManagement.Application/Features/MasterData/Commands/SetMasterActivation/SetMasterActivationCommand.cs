public sealed record SetMasterActivationCommandResult(bool IsActive);

/// Masters are never deleted (rule 9): deactivate hides a value from new records, old ones keep it.
public sealed record SetMasterActivationCommand(string Type, Guid Id, bool IsActive) : ICommand<Result<SetMasterActivationCommandResult>>;
