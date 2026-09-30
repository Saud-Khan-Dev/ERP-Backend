public class AppealActionsHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateAppealCommand, Result<AppealActionResult>>,
    ICommandHandler<StartAppealHearingCommand, Result<AppealActionResult>>,
    ICommandHandler<DecideAppealCommand, Result<AppealActionResult>>,
    ICommandHandler<WithdrawAppealCommand, Result<AppealActionResult>>
{
  public async Task<Result<AppealActionResult>> Handle(UpdateAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    var warnings = appeal.Update(await command.Appeal.ToFilingAsync(context, cancellationToken));
    return await SaveAsync(appeal, warnings, cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(StartAppealHearingCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    appeal.StartHearing();
    return await SaveAsync(appeal, Array.Empty<string>(), cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(DecideAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    appeal.Decide(command.DecisionDate, command.DecisionOutcome, command.DecisionDetails);

    var warnings = command.DecisionDate > appeal.DecisionDueDate
      ? new[] { $"Decided {command.DecisionDate.DayNumber - appeal.DecisionDueDate.DayNumber} days after the {PropertyAppeal.DecisionWindowDays}-day limit of Act s.32(1)." }
      : Array.Empty<string>();

    return await SaveAsync(appeal, warnings, cancellationToken);
  }

  public async Task<Result<AppealActionResult>> Handle(WithdrawAppealCommand command, CancellationToken cancellationToken)
  {
    var appeal = await context.LoadAppealAsync(command.Id, cancellationToken);
    appeal.Withdraw(command.Remarks);
    return await SaveAsync(appeal, Array.Empty<string>(), cancellationToken);
  }

  private async Task<Result<AppealActionResult>> SaveAsync(PropertyAppeal appeal, IReadOnlyList<string> warnings, CancellationToken cancellationToken)
  {
    await context.SaveChangesAsync(cancellationToken);
    return Result<AppealActionResult>.Success(new AppealActionResult(appeal.Id.Value, appeal.AppealStatus, appeal.DecisionDueDate, warnings));
  }
}
