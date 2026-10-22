using System.Globalization;
using Microsoft.EntityFrameworkCore;

/// What the register's filter needs from outside the database query: tags matching the search (a pattern search on
/// the tag column), the department with its sub-units (from HRM) and today's date.
public sealed record AssetFilterContext(IReadOnlyList<Guid>? TagMatches, IReadOnlyList<Guid>? DepartmentIds, DateOnly Today, DateTime Now)
{
  public static async Task<AssetFilterContext> BuildAsync(GetAssetsQuery query, IAssetDataQueries queries, IHrmDirectory hrm, IClock clock, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid>? tags = null;
    if (!string.IsNullOrWhiteSpace(query.Search))
      tags = await queries.AssetIdsWithTagLikeAsync(query.Search, cancellationToken);

    IReadOnlyList<Guid>? departments = null;
    if (query.DepartmentId is { } department)
    {
      departments = [department];
      if (query.IncludeSubUnits && hrm.IsConfigured)
      {
        var units = await hrm.OrgUnitsAsync(cancellationToken);
        var below = new List<Guid> { department };
        for (var i = 0; i < below.Count; i++)
          below.AddRange(units.Where(u => u.ParentId == below[i] && !below.Contains(u.Id)).Select(u => u.Id));
        departments = below;
      }
    }

    return new AssetFilterContext(tags, departments, clock.Today, clock.UtcNow);
  }
}

/// The register's filters and sort order, shared by the paged register (GET /assets) and the reports
/// (GET /asset-reports/register) so a report always lists exactly what the register shows for the same filters.
public static class AssetQueryFilter
{
  public static async Task<IQueryable<Asset>> ApplyAsync(IApplicationDbContext context, GetAssetsQuery query, AssetFilterContext extra, CancellationToken cancellationToken)
  {
    var assets = context.Assets.AsNoTracking();

    if (query.AssetClassId.HasValue)
    {
      var classId = AssetClassId.Of(query.AssetClassId.Value);
      assets = assets.Where(a => a.AssetClassId == classId);
    }

    if (query.AssetTypeId.HasValue)
    {
      var typeId = AssetTypeId.Of(query.AssetTypeId.Value);
      assets = assets.Where(a => a.AssetTypeId == typeId);
    }

    if (query.CategoryId.HasValue)
    {
      var categoryId = AssetCategoryId.Of(query.CategoryId.Value);
      if (query.IncludeSubCategories)
      {
        var root = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
          ?? throw new DomainException("The category in the filter was not found. Clear the filter.");

        // subtree = the node plus everything under its materialized path
        var subtree = (await context.AssetCategories.AsNoTracking()
            .Where(c => c.AssetTypeId == root.AssetTypeId)
            .ToListAsync(cancellationToken))
          .Where(c => c.Id == root.Id || root.IsAncestorOf(c))
          .Select(c => c.Id)
          .ToList();

        assets = assets.Where(a => subtree.Contains(a.CategoryId));
      }
      else
      {
        assets = assets.Where(a => a.CategoryId == categoryId);
      }
    }

    if (query.StatusId.HasValue)
    {
      var statusId = AssetStatusId.Of(query.StatusId.Value);
      assets = assets.Where(a => a.StatusId == statusId);
    }

    if (query.NoLocation)
      assets = assets.Where(a => a.CurrentLocationId == null);

    if (query.LocationId.HasValue)
    {
      var locationId = LocationId.Of(query.LocationId.Value);
      if (query.IncludeSubLocations)
      {
        // the location and every location under it (materialized path)
        var root = await context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken)
          ?? throw new DomainException("The location in the filter was not found. Clear the filter.");
        var prefix = root.Path + Location.PathSeparator;
        var subtree = (await context.Locations.AsNoTracking().ToListAsync(cancellationToken))
          .Where(l => l.Id == root.Id || (root.Path != null && l.Path != null && l.Path.StartsWith(prefix, StringComparison.Ordinal)))
          .Select(l => (LocationId?)l.Id)
          .ToList();
        assets = assets.Where(a => subtree.Contains(a.CurrentLocationId));
      }
      else
      {
        assets = assets.Where(a => a.CurrentLocationId == locationId);
      }
    }

    if (query.MissingRequiredLocation)
    {
      var requiring = context.AssetTypes.AsNoTracking().Where(t => t.RequiresLocation).Select(t => t.Id);
      assets = assets.Where(a => a.CurrentLocationId == null && requiring.Contains(a.AssetTypeId));
    }

    if (query.CustodianId.HasValue)
      assets = assets.Where(a => a.CustodianId == query.CustodianId.Value);
    if (query.HasCustodian == true)
      assets = assets.Where(a => a.CustodianId != null);
    if (query.HasCustodian == false)
      assets = assets.Where(a => a.CustodianId == null);

    if (extra.DepartmentIds is { Count: > 0 } departments)
      assets = assets.Where(a => a.DepartmentId != null && departments.Contains(a.DepartmentId.Value));

    if (query.Ownership.HasValue)
      assets = assets.Where(a => a.Ownership == query.Ownership.Value);

    // "by date and time": when the asset was registered
    if (query.RegisteredFrom.HasValue)
    {
      var from = DateTime.SpecifyKind(query.RegisteredFrom.Value, DateTimeKind.Utc);
      assets = assets.Where(a => a.CreatedAt >= from);
    }

    if (query.RegisteredTo.HasValue)
    {
      var to = DateTime.SpecifyKind(query.RegisteredTo.Value, DateTimeKind.Utc);
      assets = assets.Where(a => a.CreatedAt <= to);
    }

    var acquisitions = context.AssetAcquisitions.AsNoTracking();
    if (query.AcquiredFrom.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionDate >= query.AcquiredFrom.Value));
    if (query.AcquiredTo.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionDate <= query.AcquiredTo.Value));
    if (query.CostMin.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionCost >= query.CostMin.Value));
    if (query.CostMax.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionCost <= query.CostMax.Value));
    if (query.HasPurchase == true)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id));
    if (query.HasPurchase == false)
      assets = assets.Where(a => !acquisitions.Any(q => q.AssetId == a.Id));

    // the warranty of the purchase record: ends between two dates (a window like "the next 30 days", or "ended")
    if (query.WarrantyFrom.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.WarrantyExpiryDate != null && q.WarrantyExpiryDate >= query.WarrantyFrom.Value));
    if (query.WarrantyTo.HasValue)
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.WarrantyExpiryDate != null && q.WarrantyExpiryDate <= query.WarrantyTo.Value));

    // disposed = the disposal was approved (a proposal waiting, or one rejected, withdrawn or cancelled, is not)
    var disposals = context.AssetDisposals.AsNoTracking().Where(d => d.State == DisposalState.Approved);
    if (query.Disposed == DisposalFilter.Exclude)
      assets = assets.Where(a => !disposals.Any(d => d.AssetId == a.Id));
    else if (query.Disposed == DisposalFilter.Only)
      assets = assets.Where(a => disposals.Any(d => d.AssetId == a.Id));
    else if (query.Disposed == DisposalFilter.Proposed)
    {
      var proposals = context.AssetDisposals.AsNoTracking().Where(d => d.State == DisposalState.Proposed);
      assets = assets.Where(a => proposals.Any(d => d.AssetId == a.Id));
    }
    if (query.DisposedFrom.HasValue)
      assets = assets.Where(a => disposals.Any(d => d.AssetId == a.Id && d.DisposalDate >= query.DisposedFrom.Value));
    if (query.DisposedTo.HasValue)
      assets = assets.Where(a => disposals.Any(d => d.AssetId == a.Id && d.DisposalDate <= query.DisposedTo.Value));

    // held on a date: acquired (or, with no purchase record, registered) by then, and not yet disposed of
    if (query.HeldOn is { } heldOn)
    {
      var endOfDay = heldOn.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);
      assets = assets.Where(a =>
          (acquisitions.Any(q => q.AssetId == a.Id && q.AcquisitionDate <= heldOn)
           || (!acquisitions.Any(q => q.AssetId == a.Id) && a.CreatedAt <= endOfDay))
          && !disposals.Any(d => d.AssetId == a.Id && d.DisposalDate <= heldOn));
    }

    var assignments = context.AssetAssignments.AsNoTracking();
    if (query.Loan == LoanFilter.Out)
      assets = assets.Where(a => assignments.Any(x => x.AssetId == a.Id && x.ExpectedReturnDate != null && x.ActualReturnDate == null));
    else if (query.Loan == LoanFilter.Overdue)
    {
      var today = extra.Today;
      assets = assets.Where(a => assignments.Any(x => x.AssetId == a.Id && x.ExpectedReturnDate != null && x.ActualReturnDate == null && x.ExpectedReturnDate < today));
    }

    // a running schedule whose last ended month (last month) is not posted
    if (query.DepreciationBehind)
    {
      var lastMonthEnd = new DateOnly(extra.Today.Year, extra.Today.Month, 1).AddDays(-1);
      var dayAfter = lastMonthEnd.AddDays(1);
      var schedules = context.AssetDepreciationSchedules.AsNoTracking();
      var entries = context.AssetDepreciationEntries.AsNoTracking();
      assets = assets.Where(a => schedules.Any(s =>
          s.AssetId == a.Id && s.IsActive && s.StartDate <= lastMonthEnd && s.EndDate > lastMonthEnd
          && !entries.Any(e => e.ScheduleId == s.Id && e.Posted && e.ReversedAt == null && e.PeriodEnd >= dayAfter)));
    }

    // out of service: in a status that neither ends the record nor lets it be issued (under repair ...)
    if (query.OutOfService)
    {
      var outOfService = context.AssetStatuses.AsNoTracking().Where(s => !s.IsTerminal && !s.AllowsAssignment).Select(s => s.Id);
      assets = assets.Where(a => outOfService.Contains(a.StatusId));
    }

    // the warranty ends between today and N days from now
    if (query.WarrantyWithinDays is { } within)
    {
      var today = extra.Today;
      var until = today.AddDays(within);
      assets = assets.Where(a => acquisitions.Any(q => q.AssetId == a.Id && q.WarrantyExpiryDate != null && q.WarrantyExpiryDate >= today && q.WarrantyExpiryDate <= until));
    }

    // in its current status since before a date ("under repair for more than 30 days"); InStatusOverDays counts back from now
    DateTime? statusSince = query.StatusSinceTo is { } sinceTo ? DateTime.SpecifyKind(sinceTo, DateTimeKind.Utc) : null;
    if (query.InStatusOverDays is { } days)
    {
      var cut = extra.Now.AddDays(-days);
      statusSince = statusSince is { } given && given < cut ? given : cut;
    }
    if (statusSince is { } statusCut)
    {
      var events = context.AssetLifecycleEvents.AsNoTracking();
      assets = assets.Where(a =>
          (events.Where(e => e.AssetId == a.Id && e.ToStatusId == a.StatusId).Max(e => (DateTime?)e.EventDate) ?? a.CreatedAt) <= statusCut);
    }

    if (!query.IncludeInactive)
      assets = assets.Where(a => a.IsActive);

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      var upper = term.ToUpperInvariant();
      var lower = term.ToLowerInvariant();
      var tagIds = (extra.TagMatches ?? []).Select(AssetId.Of).ToList();

      // fields set up as "Search": their typed values (and the labels of their choices)
      var searchable = context.AttributeAssignments.AsNoTracking().Where(s => s.IsSearchable && s.IsActive && !s.IsHidden).Select(s => s.AttributeDefinitionId);
      var values = context.AssetAttributeValues.AsNoTracking();
      var choices = context.OptionSetValues.AsNoTracking();

      assets = assets.Where(a =>
          tagIds.Contains(a.Id)
          || a.Name.Value.ToUpper().Contains(upper)
          || (a.Barcode != null && a.Barcode.ToUpper().Contains(upper))
          || (a.Description != null && a.Description.ToLower().Contains(lower))
          || (a.CustodianName != null && a.CustodianName.ToUpper().Contains(upper))
          || (a.CustodianNumber != null && a.CustodianNumber.ToUpper().Contains(upper))
          || values.Any(v => v.AssetId == a.Id && searchable.Contains(v.AttributeDefinitionId)
              && ((v.ValueText != null && v.ValueText.ToUpper().Contains(upper))
                  || (v.OptionValueId != null && choices.Any(o => o.Id == v.OptionValueId && o.Label.ToUpper().Contains(upper))))));
    }

    foreach (var filter in query.AttributeFilters ?? Array.Empty<AttributeFilter>())
      assets = await ApplyAttributeFilterAsync(context, assets, filter, cancellationToken);

    return assets;
  }

  public static IOrderedQueryable<Asset> Order(IApplicationDbContext context, IQueryable<Asset> assets, GetAssetsQuery query)
  {
    var acquisitions = context.AssetAcquisitions.AsNoTracking();
    var statuses = context.AssetStatuses.AsNoTracking();
    var locations = context.Locations.AsNoTracking();
    var desc = query.SortDescending;
    IOrderedQueryable<Asset> ordered = query.SortBy switch
    {
      AssetSort.Name => desc ? assets.OrderByDescending(a => a.Name.Value) : assets.OrderBy(a => a.Name.Value),
      AssetSort.Registered => desc ? assets.OrderByDescending(a => a.CreatedAt) : assets.OrderBy(a => a.CreatedAt),
      // assets without a purchase record always come last, whichever direction
      AssetSort.Acquired => desc
        ? assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenByDescending(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (DateOnly?)q.AcquisitionDate).FirstOrDefault())
        : assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenBy(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (DateOnly?)q.AcquisitionDate).FirstOrDefault()),
      AssetSort.Cost => desc
        ? assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenByDescending(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (decimal?)q.AcquisitionCost).FirstOrDefault())
        : assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id)).ThenBy(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => (decimal?)q.AcquisitionCost).FirstOrDefault()),
      AssetSort.Warranty => desc
        ? assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id && q.WarrantyExpiryDate != null)).ThenByDescending(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => q.WarrantyExpiryDate).FirstOrDefault())
        : assets.OrderBy(a => !acquisitions.Any(q => q.AssetId == a.Id && q.WarrantyExpiryDate != null)).ThenBy(a => acquisitions.Where(q => q.AssetId == a.Id).Select(q => q.WarrantyExpiryDate).FirstOrDefault()),
      AssetSort.Status => desc
        ? assets.OrderByDescending(a => statuses.Where(s => s.Id == a.StatusId).Select(s => s.DisplayOrder).FirstOrDefault())
        : assets.OrderBy(a => statuses.Where(s => s.Id == a.StatusId).Select(s => s.DisplayOrder).FirstOrDefault()),
      // assets with no location come last
      AssetSort.Location => desc
        ? assets.OrderBy(a => a.CurrentLocationId == null).ThenByDescending(a => locations.Where(l => l.Id == a.CurrentLocationId).Select(l => l.Path).FirstOrDefault())
        : assets.OrderBy(a => a.CurrentLocationId == null).ThenBy(a => locations.Where(l => l.Id == a.CurrentLocationId).Select(l => l.Path).FirstOrDefault()),
      _ => desc ? assets.OrderByDescending(a => a.AssetCode) : assets.OrderBy(a => a.AssetCode),
    };

    return ordered.ThenBy(a => a.AssetCode);
  }

  private static async Task<IQueryable<Asset>> ApplyAttributeFilterAsync(IApplicationDbContext context, IQueryable<Asset> assets, AttributeFilter filter, CancellationToken cancellationToken)
  {
    AttributeCode code;
    try { code = AttributeCode.Of(filter.Code); }
    catch (DomainException) { throw new DomainException("One of the field filters names a field that does not exist. Clear the field filters."); }

    var definition = await context.AttributeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, cancellationToken)
      ?? throw new DomainException("One of the field filters names a field that does not exist. Clear the field filters.");

    // only fields set up as "Filter" (or "Search") keep the typed values a filter reads
    var definitionId = definition.Id;
    if (!await context.AttributeAssignments.AnyAsync(a => a.AttributeDefinitionId == definitionId && !a.IsHidden && (a.IsFilterable || a.IsSearchable), cancellationToken))
      throw new DomainException($"'{definition.Name.Value}' is not set up as a filter. Turn on \"Filter\" for it in Assets › Configuration first.");

    var values = context.AssetAttributeValues.Where(v => v.AttributeDefinitionId == definitionId);
    var op = filter.Operator;
    var label = definition.Name.Value;

    switch (definition.DataType)
    {
      case AttributeDataType.Integer:
      case AttributeDataType.Decimal:
      {
        if (!decimal.TryParse(filter.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
          throw new DomainException($"The filter value for '{label}' must be a number.");

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueNumber == number),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueNumber != number),
          AttributeFilterOperator.Gt => values.Where(v => v.ValueNumber > number),
          AttributeFilterOperator.Gte => values.Where(v => v.ValueNumber >= number),
          AttributeFilterOperator.Lt => values.Where(v => v.ValueNumber < number),
          AttributeFilterOperator.Lte => values.Where(v => v.ValueNumber <= number),
          _ => throw new DomainException($"'{label}' is a number: compare it with =, ≠, >, ≥, < or ≤.")
        };
        break;
      }

      case AttributeDataType.Date:
      case AttributeDataType.DateTime:
      {
        if (!DateTime.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
          throw new DomainException($"The filter value for '{label}' must be a date.");

        var date = DateOnly.FromDateTime(moment);
        var isDate = definition.DataType == AttributeDataType.Date;

        values = op switch
        {
          AttributeFilterOperator.Eq => isDate ? values.Where(v => v.ValueDate == date) : values.Where(v => v.ValueDatetime == moment),
          AttributeFilterOperator.Neq => isDate ? values.Where(v => v.ValueDate != date) : values.Where(v => v.ValueDatetime != moment),
          AttributeFilterOperator.Gt => isDate ? values.Where(v => v.ValueDate > date) : values.Where(v => v.ValueDatetime > moment),
          AttributeFilterOperator.Gte => isDate ? values.Where(v => v.ValueDate >= date) : values.Where(v => v.ValueDatetime >= moment),
          AttributeFilterOperator.Lt => isDate ? values.Where(v => v.ValueDate < date) : values.Where(v => v.ValueDatetime < moment),
          AttributeFilterOperator.Lte => isDate ? values.Where(v => v.ValueDate <= date) : values.Where(v => v.ValueDatetime <= moment),
          _ => throw new DomainException($"'{label}' is a date: compare it with on, before or after.")
        };
        break;
      }

      case AttributeDataType.Boolean:
      {
        if (!bool.TryParse(filter.Value, out var flag))
          throw new DomainException($"The filter value for '{label}' must be yes or no.");

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueBoolean == flag),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueBoolean != flag),
          _ => throw new DomainException($"'{label}' is a yes / no field: filter on 'is' or 'is not'.")
        };
        break;
      }

      default:
      {
        var text = definition.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect
          ? filter.Value.Trim().ToUpperInvariant()
          : filter.Value.Trim();

        values = op switch
        {
          AttributeFilterOperator.Eq => values.Where(v => v.ValueText == text),
          AttributeFilterOperator.Neq => values.Where(v => v.ValueText != text),
          AttributeFilterOperator.Contains => values.Where(v => v.ValueText != null && v.ValueText.ToUpper().Contains(text.ToUpperInvariant())),
          _ => throw new DomainException($"'{label}' is text: filter on 'is', 'is not' or 'contains'.")
        };
        break;
      }
    }

    return assets.Where(a => values.Any(v => v.AssetId == a.Id));
  }
}
