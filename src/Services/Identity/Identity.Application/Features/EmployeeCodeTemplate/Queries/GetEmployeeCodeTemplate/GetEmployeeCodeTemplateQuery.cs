public sealed record GetEmployeeCodeTemplateQueryResult(EmployeeCodeTemplateDto Template);

public sealed record GetEmployeeCodeTemplateQuery : IQuery<Result<GetEmployeeCodeTemplateQueryResult>>;
