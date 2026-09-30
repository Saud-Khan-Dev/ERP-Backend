public sealed record CancelTransferCommandResult(TransferStatus Status);

public sealed record CancelTransferCommand(Guid Id, string? Reason = null) : ICommand<Result<CancelTransferCommandResult>>;
