using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One line of the change log. ChangedBy: the Identity user id (show the person's name through the user directory);
/// ChangedByName: the account name as it stood.
public sealed record ChangeDto(DateTime ChangedAt, Guid? ChangedBy, string? ChangedByName, string Entity, Guid EntityId, string? EntityCode,
  string Action, string? Field, string? OldValue, string? NewValue);

public sealed record GetChangesQueryResult(PaginatedResult<ChangeDto> Changes);

/// The change log of a property (its record and every record hanging off it) or of an owner's own record, newest first.
public sealed record GetChangesQuery(PaginationRequest Pagination, Guid? PropertyId = null, Guid? OwnerId = null) : IQuery<Result<GetChangesQueryResult>>;

public class GetChangesQueryValidator : AbstractValidator<GetChangesQuery>
{
  public GetChangesQueryValidator()
  {
    RuleFor(x => x.Pagination.Pageindex).GreaterThanOrEqualTo(0);
    RuleFor(x => x.Pagination.PageSize).InclusiveBetween(1, 500);
  }
}

public class GetChangesHandler(IApplicationDbContext context) : IQueryHandler<GetChangesQuery, Result<GetChangesQueryResult>>
{
  public async Task<Result<GetChangesQueryResult>> Handle(GetChangesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.PropertyChanges.AsNoTracking();
    if (query.PropertyId is { } propertyId)
    {
      await context.EnsurePropertyExistsAsync(propertyId, cancellationToken);
      rows = rows.Where(c => c.PropertyId == propertyId);
    }
    else if (query.OwnerId is { } ownerId)
    {
      var id = OwnerId.Of(ownerId);
      if (!await context.Owners.AnyAsync(o => o.Id == id, cancellationToken))
        throw new OwnerNotFoundException($"Owner {ownerId} was not found.");
      rows = rows.Where(c => c.OwnerId == ownerId);
    }

    var total = await rows.CountAsync(cancellationToken);
    var page = await rows.OrderByDescending(c => c.ChangedAt).ThenBy(c => c.Entity).ThenBy(c => c.Field)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
        .Select(c => new ChangeDto(c.ChangedAt, c.ChangedBy, c.ChangedByName, c.Entity, c.EntityId, c.EntityCode, c.Action, c.Field, c.OldValue, c.NewValue))
        .ToListAsync(cancellationToken);

    return Result<GetChangesQueryResult>.Success(new GetChangesQueryResult(
      new PaginatedResult<ChangeDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, page)));
  }
}
