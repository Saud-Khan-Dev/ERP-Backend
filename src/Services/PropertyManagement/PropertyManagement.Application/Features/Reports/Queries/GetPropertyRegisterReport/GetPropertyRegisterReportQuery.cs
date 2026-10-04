using FluentValidation;

public sealed record GetPropertyRegisterReportQueryResult(int Count, IReadOnlyList<PropertyReportRowDto> Rows, PropertyReportTotalsDto Totals);

/// The properties a report covers: either the ones picked by id (in the register's order, active or not), or
/// every property matching the register's filters (the same as GET /properties, without paging).
public sealed record GetPropertyRegisterReportQuery(GetPropertiesQuery Filter, IReadOnlyList<Guid>? Ids = null)
  : IQuery<Result<GetPropertyRegisterReportQueryResult>>;

public class GetPropertyRegisterReportQueryValidator : AbstractValidator<GetPropertyRegisterReportQuery>
{
  public const int MaxIds = 150;

  public GetPropertyRegisterReportQueryValidator()
  {
    RuleFor(x => x.Filter).NotNull().SetValidator(new PropertyFilterRules());
    // the ids travel in the query string (ids=…&ids=…), which the server caps at 8 KB - about 190 ids
    RuleFor(x => x.Ids).Must(ids => ids is null || ids.Count <= MaxIds)
      .WithMessage($"Pick at most {MaxIds} properties by id; for more, run the report on the register's filters.");
  }
}
