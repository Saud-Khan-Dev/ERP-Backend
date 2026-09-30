public sealed record SetOwnerActivationCommandResult(bool IsActive);

public sealed record SetOwnerActivationCommand(Guid Id, bool IsActive) : ICommand<Result<SetOwnerActivationCommandResult>>;
