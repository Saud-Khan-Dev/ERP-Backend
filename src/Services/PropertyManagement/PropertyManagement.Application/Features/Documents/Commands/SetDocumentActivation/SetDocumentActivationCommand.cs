public sealed record SetDocumentActivationCommandResult(bool IsActive);

/// Documents are never deleted; a document filed in error is deactivated. The file stays on the server.
public sealed record SetDocumentActivationCommand(Guid Id, bool IsActive) : ICommand<Result<SetDocumentActivationCommandResult>>;
