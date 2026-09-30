public sealed record EnforceEncumbranceCommandResult(EncumbranceStatus Status);

public sealed record EnforceEncumbranceCommand(Guid Id, string? Remarks = null) : ICommand<Result<EnforceEncumbranceCommandResult>>;
