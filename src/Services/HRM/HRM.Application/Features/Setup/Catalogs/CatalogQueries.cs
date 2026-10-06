using Microsoft.EntityFrameworkCore;

public sealed record GetOrganizationUnitTypesQueryResult(IReadOnlyList<OrganizationUnitTypeDto> UnitTypes);
public sealed record GetOrganizationUnitTypesQuery(bool IncludeInactive) : IQuery<Result<GetOrganizationUnitTypesQueryResult>>;

public sealed record GetLocationsQueryResult(IReadOnlyList<LocationDto> Locations);
public sealed record GetLocationsQuery(bool IncludeInactive) : IQuery<Result<GetLocationsQueryResult>>;

public sealed record GetDesignationsQueryResult(IReadOnlyList<DesignationDto> Designations);
public sealed record GetDesignationsQuery(bool IncludeInactive) : IQuery<Result<GetDesignationsQueryResult>>;

public sealed record GetPayScaleGradesQueryResult(IReadOnlyList<PayScaleGradeDto> Grades);
public sealed record GetPayScaleGradesQuery(bool IncludeInactive) : IQuery<Result<GetPayScaleGradesQueryResult>>;

public sealed record GetDocumentTypesQueryResult(IReadOnlyList<DocumentTypeDto> DocumentTypes);
public sealed record GetDocumentTypesQuery(bool IncludeInactive) : IQuery<Result<GetDocumentTypesQueryResult>>;

public sealed record GetRecruitmentMethodsQueryResult(IReadOnlyList<RecruitmentMethodDto> RecruitmentMethods);
public sealed record GetRecruitmentMethodsQuery(bool IncludeInactive) : IQuery<Result<GetRecruitmentMethodsQueryResult>>;

public sealed record GetServiceEventTypesQueryResult(IReadOnlyList<ServiceEventTypeDto> EventTypes);
public sealed record GetServiceEventTypesQuery(bool IncludeInactive) : IQuery<Result<GetServiceEventTypesQueryResult>>;

public sealed record GetRequestTypesQueryResult(IReadOnlyList<EmployeeRequestTypeDto> RequestTypes);
public sealed record GetRequestTypesQuery(bool IncludeInactive) : IQuery<Result<GetRequestTypesQueryResult>>;

public class CatalogQueryHandlers(IApplicationDbContext context) :
  IQueryHandler<GetOrganizationUnitTypesQuery, Result<GetOrganizationUnitTypesQueryResult>>,
  IQueryHandler<GetLocationsQuery, Result<GetLocationsQueryResult>>,
  IQueryHandler<GetDesignationsQuery, Result<GetDesignationsQueryResult>>,
  IQueryHandler<GetPayScaleGradesQuery, Result<GetPayScaleGradesQueryResult>>,
  IQueryHandler<GetDocumentTypesQuery, Result<GetDocumentTypesQueryResult>>,
  IQueryHandler<GetRecruitmentMethodsQuery, Result<GetRecruitmentMethodsQueryResult>>,
  IQueryHandler<GetServiceEventTypesQuery, Result<GetServiceEventTypesQueryResult>>,
  IQueryHandler<GetRequestTypesQuery, Result<GetRequestTypesQueryResult>>
{
  public async Task<Result<GetOrganizationUnitTypesQueryResult>> Handle(GetOrganizationUnitTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.OrganizationUnitTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive)
      .OrderBy(x => x.HierarchyLevel).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetOrganizationUnitTypesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetLocationsQueryResult>> Handle(GetLocationsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.Locations.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetLocationsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetDesignationsQueryResult>> Handle(GetDesignationsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.Designations.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Title).ToListAsync(cancellationToken);
    return Result<GetDesignationsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetPayScaleGradesQueryResult>> Handle(GetPayScaleGradesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.PayScaleGrades.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.BpsNumber).ToListAsync(cancellationToken);
    return Result<GetPayScaleGradesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetDocumentTypesQueryResult>> Handle(GetDocumentTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.DocumentTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetDocumentTypesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetRecruitmentMethodsQueryResult>> Handle(GetRecruitmentMethodsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.RecruitmentMethods.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetRecruitmentMethodsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetServiceEventTypesQueryResult>> Handle(GetServiceEventTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.ServiceEventTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive)
      .OrderBy(x => x.Category).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetServiceEventTypesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetRequestTypesQueryResult>> Handle(GetRequestTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.RequestTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetRequestTypesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }
}
