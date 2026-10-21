using Microsoft.EntityFrameworkCore;

public class GetAttentionHandler(IApplicationDbContext context, IAssetDataQueries queries, IHrmDirectory hrm, ICurrentUser currentUser, IClock clock)
  : IQueryHandler<GetAttentionQuery, Result<AttentionResponse>>
{
  public const int OutOfServiceDays = 30;
  public const int WarrantyDays = 30;

  public async Task<Result<AttentionResponse>> Handle(GetAttentionQuery query, CancellationToken cancellationToken)
  {
    var items = new List<AttentionItemDto>();
    var canEdit = currentUser.HasPermission(PermissionCatalog.Assets.Edit);
    var seesMoney = currentUser.HasPermission(PermissionCatalog.AssetFinance.View);
    var today = clock.Today;

    if (canEdit)
    {
      var overdue = Filter(q => q with { Loan = LoanFilter.Overdue });
      var (count, _) = await CountAsync(overdue, cancellationToken);
      var oldest = count == 0 ? null : await context.AssetAssignments.AsNoTracking()
        .Where(x => x.ExpectedReturnDate != null && x.ActualReturnDate == null && x.ExpectedReturnDate < today)
        .Join(context.Assets.AsNoTracking().Where(a => a.IsActive), x => x.AssetId, a => a.Id, (x, _) => x.ExpectedReturnDate)
        .MinAsync(cancellationToken);
      items.Add(new AttentionItemDto("assets.loans-overdue", count, oldest is { } day ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null));

      items.Add(new AttentionItemDto("assets.location-missing", (await CountAsync(Filter(q => q with { MissingRequiredLocation = true }), cancellationToken)).Count));
      items.Add(new AttentionItemDto("assets.out-of-service-long",
        (await CountAsync(Filter(q => q with { OutOfService = true, InStatusOverDays = OutOfServiceDays }), cancellationToken)).Count));

      var warranty = Filter(q => q with { WarrantyWithinDays = WarrantyDays });
      var (warrantyCount, warrantyAssets) = await CountAsync(warranty, cancellationToken);
      DateOnly? nextDue = null;
      if (warrantyCount > 0)
      {
        var ids = warrantyAssets.Select(a => a.Id);
        nextDue = await context.AssetAcquisitions.AsNoTracking()
          .Where(q => ids.Contains(q.AssetId) && q.WarrantyExpiryDate != null && q.WarrantyExpiryDate >= today)
          .MinAsync(q => q.WarrantyExpiryDate, cancellationToken);
      }
      items.Add(new AttentionItemDto("assets.warranty-ending", warrantyCount, NextDue: nextDue));
    }

    if (seesMoney && currentUser.HasPermission(PermissionCatalog.AssetFinance.Post))
      items.Add(new AttentionItemDto("assets.depreciation-behind", (await CountAsync(Filter(q => q with { DepreciationBehind = true }), cancellationToken)).Count));

    if (seesMoney && currentUser.HasPermission(PermissionCatalog.AssetFinance.Create))
      items.Add(new AttentionItemDto("assets.no-purchase", (await CountAsync(Filter(q => q with { HasPurchase = false }), cancellationToken)).Count));

    if (currentUser.EmployeeId is { } employeeId && currentUser.HasPermission(PermissionCatalog.Assets.View))
      items.Add(new AttentionItemDto("assets.in-my-custody",
        (await CountAsync(Filter(q => q with { CustodianId = employeeId }), cancellationToken)).Count, ForMe: true));

    return Result<AttentionResponse>.Success(new AttentionResponse(new AttentionDto(clock.UtcNow, items)));
  }

  /// The register's query for a queue: not disposed, not switched off.
  private static GetAssetsQuery Filter(Func<GetAssetsQuery, GetAssetsQuery> shape) =>
    shape(new GetAssetsQuery(new PaginationRequest(0, 1), Disposed: DisposalFilter.Exclude, IncludeMoney: true));

  private async Task<(int Count, IQueryable<Asset> Assets)> CountAsync(GetAssetsQuery filter, CancellationToken cancellationToken)
  {
    var extra = await AssetFilterContext.BuildAsync(filter, queries, hrm, clock, cancellationToken);
    var assets = await AssetQueryFilter.ApplyAsync(context, filter, extra, cancellationToken);
    return (await assets.CountAsync(cancellationToken), assets);
  }
}
