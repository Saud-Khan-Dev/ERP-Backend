/// Edits the numbering scheme. Codes already on accounts are left alone; the change applies to
/// codes issued or edited from now on.
public class UpdateEmployeeCodeTemplateHandler(IApplicationDbContext context, EmployeeCodeService employeeCodes)
  : ICommandHandler<UpdateEmployeeCodeTemplateCommand, Result<UpdateEmployeeCodeTemplateCommandResult>>
{
  public async Task<Result<UpdateEmployeeCodeTemplateCommandResult>> Handle(UpdateEmployeeCodeTemplateCommand command, CancellationToken cancellationToken)
  {
    var input = command.Template;

    var template = await employeeCodes.GetTemplateAsync(cancellationToken);
    template.Update(input.Prefix, input.Separator, input.MinimumDigits, input.NextNumber);

    var conflict = await employeeCodes.FindTemplateConflictAsync(template, cancellationToken);
    if (conflict is not null)
      return Result<UpdateEmployeeCodeTemplateCommandResult>.Failure(conflict);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateEmployeeCodeTemplateCommandResult>.Success(
      new UpdateEmployeeCodeTemplateCommandResult(template.ToDto()));
  }
}
