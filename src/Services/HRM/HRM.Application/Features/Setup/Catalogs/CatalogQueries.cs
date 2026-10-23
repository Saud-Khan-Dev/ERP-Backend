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

public sealed record GetQualificationLevelsQueryResult(IReadOnlyList<QualificationLevelDto> QualificationLevels);
public sealed record GetQualificationLevelsQuery(bool IncludeInactive) : IQuery<Result<GetQualificationLevelsQueryResult>>;

public sealed record GetFamilyRelationsQueryResult(IReadOnlyList<FamilyRelationDto> FamilyRelations);
public sealed record GetFamilyRelationsQuery(bool IncludeInactive) : IQuery<Result<GetFamilyRelationsQueryResult>>;

public sealed record GetEmploymentMethodsQueryResult(IReadOnlyList<EmploymentMethodDto> EmploymentMethods);
public sealed record GetEmploymentMethodsQuery(bool IncludeInactive) : IQuery<Result<GetEmploymentMethodsQueryResult>>;

public sealed record GetAdjustmentTypesQueryResult(IReadOnlyList<AdjustmentTypeDto> AdjustmentTypes);
public sealed record GetAdjustmentTypesQuery(bool IncludeInactive) : IQuery<Result<GetAdjustmentTypesQueryResult>>;

public sealed record GetEmploymentTypesQueryResult(IReadOnlyList<EmploymentTypeDto> EmploymentTypes);
public sealed record GetEmploymentTypesQuery(bool IncludeInactive) : IQuery<Result<GetEmploymentTypesQueryResult>>;

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
  IQueryHandler<GetEmploymentTypesQuery, Result<GetEmploymentTypesQueryResult>>,
  IQueryHandler<GetQualificationLevelsQuery, Result<GetQualificationLevelsQueryResult>>,
  IQueryHandler<GetFamilyRelationsQuery, Result<GetFamilyRelationsQueryResult>>,
  IQueryHandler<GetEmploymentMethodsQuery, Result<GetEmploymentMethodsQueryResult>>,
  IQueryHandler<GetAdjustmentTypesQuery, Result<GetAdjustmentTypesQueryResult>>,
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

  public async Task<Result<GetQualificationLevelsQueryResult>> Handle(GetQualificationLevelsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.QualificationLevels.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Rank).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetQualificationLevelsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetFamilyRelationsQueryResult>> Handle(GetFamilyRelationsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.FamilyRelations.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetFamilyRelationsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetEmploymentMethodsQueryResult>> Handle(GetEmploymentMethodsQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.EmploymentMethods.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetEmploymentMethodsQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetAdjustmentTypesQueryResult>> Handle(GetAdjustmentTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.AdjustmentTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    return Result<GetAdjustmentTypesQueryResult>.Success(new(rows.Select(x => x.ToDto()).ToList()));
  }

  public async Task<Result<GetEmploymentTypesQueryResult>> Handle(GetEmploymentTypesQuery query, CancellationToken cancellationToken)
  {
    var rows = await context.EmploymentTypes.AsNoTracking().Where(x => query.IncludeInactive || x.IsActive)
      .OrderBy(x => x.Basis).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    var used = await EmploymentTypeUse.UsedAsync(context, cancellationToken);
    return Result<GetEmploymentTypesQueryResult>.Success(new(rows.Select(x => x.ToDto(used.Contains(x.Id))).ToList()));
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
