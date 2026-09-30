public sealed record SetPropertyActivationCommandResult(bool IsActive);

/// Nothing is hard-deleted: a property is deactivated and keeps its whole history.
public sealed record SetPropertyActivationCommand(Guid Id, bool IsActive) : ICommand<Result<SetPropertyActivationCommandResult>>;
