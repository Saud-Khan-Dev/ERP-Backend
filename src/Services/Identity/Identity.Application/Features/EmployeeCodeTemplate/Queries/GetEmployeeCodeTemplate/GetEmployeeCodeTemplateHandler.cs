using Microsoft.EntityFrameworkCore;

public class GetEmployeeCodeTemplateHandler(IApplicationDbContext context)
  : IQueryHandler<GetEmployeeCodeTemplateQuery, Result<GetEmployeeCodeTemplateQueryResult>>
{
  public async Task<Result<GetEmployeeCodeTemplateQueryResult>> Handle(GetEmployeeCodeTemplateQuery query, CancellationToken cancellationToken)
  {
    var id = EmployeeCodeTemplateId.Of(EmployeeCodeTemplate.SingletonId);

    // the seeder creates the row; until it has, show the defaults that would be used
    var template = await context.EmployeeCodeTemplates.AsNoTracking()
        .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
      ?? EmployeeCodeTemplate.CreateDefault();

    return Result<GetEmployeeCodeTemplateQueryResult>.Success(new GetEmployeeCodeTemplateQueryResult(template.ToDto()));
  }
}
