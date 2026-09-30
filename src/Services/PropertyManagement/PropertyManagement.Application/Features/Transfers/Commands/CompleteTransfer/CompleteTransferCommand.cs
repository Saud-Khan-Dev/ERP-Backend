public sealed record CompleteTransferCommandResult(IReadOnlyList<Guid> ClosedOwnershipIds, IReadOnlyList<Guid> OpenedOwnershipIds);

/// Applies an approved transfer to ownership: the parties' current rows are closed and new rows opened.
/// TenureTypeId is the tenure for transferees who owned nothing before (defaults to OWNED).
public sealed record CompleteTransferCommand(Guid Id, Guid? TenureTypeId = null) : ICommand<Result<CompleteTransferCommandResult>>;
