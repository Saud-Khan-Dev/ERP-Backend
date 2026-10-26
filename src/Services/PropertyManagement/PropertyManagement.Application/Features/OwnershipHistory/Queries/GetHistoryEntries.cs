using Microsoft.EntityFrameworkCore;

public sealed record GetHistoryEntriesResult(IReadOnlyList<HistoryEntryDto> Entries);
public sealed record GetHistoryEntryResult(HistoryEntryDto Entry);

/// A property's ownership history entries (newest first); or, with no property, every entry in a status across the
/// register (Waiting: the approver's queue, oldest first).
public sealed record GetHistoryEntriesQuery(Guid? PropertyId, HistoryEntryStatus? Status) : IQuery<Result<GetHistoryEntriesResult>>;

public sealed record GetHistoryEntryQuery(Guid Id) : IQuery<Result<GetHistoryEntryResult>>;

public class GetHistoryEntriesHandler(IApplicationDbContext context, OwnershipHistoryService history)
  : IQueryHandler<GetHistoryEntriesQuery, Result<GetHistoryEntriesResult>>, IQueryHandler<GetHistoryEntryQuery, Result<GetHistoryEntryResult>>
{
  public async Task<Result<GetHistoryEntriesResult>> Handle(GetHistoryEntriesQuery query, CancellationToken cancellationToken)
  {
    var entries = context.HistoryEntries.AsNoTracking();

    if (query.PropertyId is { } id)
    {
      var propertyId = PropertyId.Of(id);
      if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
        throw new PropertyNotFoundException($"Property {id} was not found.");
      entries = entries.Where(e => e.PropertyId == propertyId);
    }

    if (query.Status is { } status)
      entries = entries.Where(e => e.Status == status);

    var rows = query.PropertyId is null
      ? await entries.OrderBy(e => e.CreatedAt).Take(500).ToListAsync(cancellationToken)
      : await entries.OrderByDescending(e => e.CreatedAt).ToListAsync(cancellationToken);

    return Result<GetHistoryEntriesResult>.Success(new GetHistoryEntriesResult(await history.ToDtosAsync(rows, cancellationToken)));
  }

  public async Task<Result<GetHistoryEntryResult>> Handle(GetHistoryEntryQuery query, CancellationToken cancellationToken)
  {
    var entry = await context.HistoryEntries.AsNoTracking().FirstOrDefaultAsync(h => h.Id == HistoryEntryId.Of(query.Id), cancellationToken)
      ?? throw new HistoryEntryNotFoundException($"Ownership history {query.Id} was not found.");
    return Result<GetHistoryEntryResult>.Success(new GetHistoryEntryResult((await history.ToDtosAsync([entry], cancellationToken))[0]));
  }
}
