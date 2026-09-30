public sealed record ReleaseEncumbranceCommandResult(EncumbranceStatus Status);

public sealed record ReleaseEncumbranceCommand(Guid Id, DateOnly ReleaseDate, string? ReleaseReferenceNo = null) : ICommand<Result<ReleaseEncumbranceCommandResult>>;
