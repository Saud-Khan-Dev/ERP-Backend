public sealed record SetOwnershipDisputeCommandResult(OwnershipStatus Status);

/// Disputed = true marks the share as contested: it is still held (it stays in the 100% total and among the
/// current owners) but cannot be transferred. False resolves the dispute and the share can be transferred again.
public sealed record SetOwnershipDisputeCommand(Guid Id, bool Disputed, string? Remarks = null) : ICommand<Result<SetOwnershipDisputeCommandResult>>;
