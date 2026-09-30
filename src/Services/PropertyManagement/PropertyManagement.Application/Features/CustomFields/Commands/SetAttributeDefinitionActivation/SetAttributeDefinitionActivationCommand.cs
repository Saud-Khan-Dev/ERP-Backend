public sealed record SetAttributeDefinitionActivationCommandResult(bool IsActive);

/// An inactive field disappears from the form; values already captured are kept.
public sealed record SetAttributeDefinitionActivationCommand(Guid Id, bool IsActive) : ICommand<Result<SetAttributeDefinitionActivationCommandResult>>;
