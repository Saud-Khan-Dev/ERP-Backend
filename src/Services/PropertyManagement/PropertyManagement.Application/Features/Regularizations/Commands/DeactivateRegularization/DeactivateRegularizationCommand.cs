public sealed record DeactivateRegularizationCommandResult(bool IsSuccess);

/// Withdraws a case entered in error; the row stays for the record.
public sealed record DeactivateRegularizationCommand(Guid Id) : ICommand<Result<DeactivateRegularizationCommandResult>>;
