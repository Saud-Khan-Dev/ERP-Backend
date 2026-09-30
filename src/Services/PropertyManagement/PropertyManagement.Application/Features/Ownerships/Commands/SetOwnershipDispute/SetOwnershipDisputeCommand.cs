public sealed record SetOwnershipDisputeCommandResult(OwnershipStatus Status);

/// Disputed = true marks the share as contested (it stops counting as current);
/// false resolves the dispute and makes it current again, if the 100% total still allows it.
public sealed record SetOwnershipDisputeCommand(Guid Id, bool Disputed, string? Remarks = null) : ICommand<Result<SetOwnershipDisputeCommandResult>>;
