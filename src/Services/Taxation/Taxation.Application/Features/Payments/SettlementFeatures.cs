using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// The bank's settlement reports: the money that reached GDA's account, compared with the payments on record. The lines
/// are given in this neutral form; reading the bank's own report format belongs to its adapter.
public sealed record ImportSettlementCommand(PaymentChannel Channel, string BatchRef, DateOnly SettlementDate, DateOnly CoversFrom, DateOnly CoversTo, IReadOnlyList<SettlementLineInput> Lines)
  : ICommand<Result<SettlementBatchDto>>;

public sealed record GetSettlementsQueryResult(PaginatedResult<SettlementBatchDto> Batches);
public sealed record GetSettlementsQuery(PaginationRequest Pagination) : IQuery<Result<GetSettlementsQueryResult>>;

/// Lines: the report's. Unsettled: payments of its period and channel that no report has settled yet.
public sealed record GetSettlementQueryResult(SettlementBatchDto Batch, IReadOnlyList<SettlementLineDto> Lines, IReadOnlyList<UnsettledPaymentDto> Unsettled);
public sealed record GetSettlementQuery(Guid Id) : IQuery<Result<GetSettlementQueryResult>>;

public class ImportSettlementCommandValidator : AbstractValidator<ImportSettlementCommand>
{
  public ImportSettlementCommandValidator()
  {
    RuleFor(x => x.BatchRef).NotEmpty().MaximumLength(60);
    RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 50000).WithMessage("A report can have at most 50,000 lines.");
    RuleForEach(x => x.Lines).ChildRules(line =>
    {
      line.RuleFor(l => l.TransactionRef).NotEmpty().MaximumLength(60);
      line.RuleFor(l => l.Psid).MaximumLength(30);
      line.RuleFor(l => l.Amount).GreaterThan(0);
    });
  }
}

public class SettlementHandlers(IApplicationDbContext context, IAuditTrail audit, IClock clock) :
  ICommandHandler<ImportSettlementCommand, Result<SettlementBatchDto>>,
  IQueryHandler<GetSettlementsQuery, Result<GetSettlementsQueryResult>>,
  IQueryHandler<GetSettlementQuery, Result<GetSettlementQueryResult>>
{
  private const string EveryTown = "A settlement report covers every town. So this needs access to every town.";

  public async Task<Result<SettlementBatchDto>> Handle(ImportSettlementCommand command, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<SettlementBatchDto>.Failure(EveryTown);

    var references = command.Lines.Select(l => l.TransactionRef.Trim()).ToList();
    var payments = await context.Payments.AsNoTracking().Where(p => p.TransactionRef != null && references.Contains(p.TransactionRef))
      .Select(p => new { Reference = p.TransactionRef!, p.Id, p.Amount, p.Psid, p.Status }).ToDictionaryAsync(p => p.Reference, p => new SettledPayment(p.Id, p.Amount, p.Psid, p.Status), cancellationToken);
    var settledBefore = (await context.SettlementLines.AsNoTracking().Where(l => l.Match == SettlementMatch.Matched && references.Contains(l.TransactionRef))
      .Select(l => l.TransactionRef).ToListAsync(cancellationToken)).ToHashSet();

    // one line for the import ("Imported", with the counts), not an automatic "Added" beside it
    using var quiet = audit.Quietly();
    var batch = SettlementBatch.Import(SettlementBatchId.New(), command.Channel, command.BatchRef, command.SettlementDate, command.CoversFrom, command.CoversTo, command.Lines,
      payments, settledBefore, clock.UtcNow);
    context.SettlementBatches.Add(batch);
    audit.Record("Imported", batch, $"{batch.LineCount} {(batch.LineCount == 1 ? "line" : "lines")}, {Rupees.Text(batch.TotalAmount)}, {batch.ExceptionCount} to look into");
    await context.SaveChangesAsync(cancellationToken);
    return Result<SettlementBatchDto>.Success(Dto(batch));
  }

  public async Task<Result<GetSettlementsQueryResult>> Handle(GetSettlementsQuery query, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<GetSettlementsQueryResult>.Failure(EveryTown);
    var rows = context.SettlementBatches.AsNoTracking();
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(b => b.SettlementDate).ThenByDescending(b => b.ImportedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    return Result<GetSettlementsQueryResult>.Success(new(new PaginatedResult<SettlementBatchDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, page.Select(Dto).ToList())));
  }

  public async Task<Result<GetSettlementQueryResult>> Handle(GetSettlementQuery query, CancellationToken cancellationToken)
  {
    if (context.IsLimited())
      return Result<GetSettlementQueryResult>.Failure(EveryTown);
    var id = SettlementBatchId.Of(query.Id);
    var batch = await context.SettlementBatches.AsNoTracking().Include(b => b.Lines).FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
      ?? throw new PaymentNotFoundException($"Settlement report {query.Id} was not found.");

    var start = Pkt.StartOf(batch.CoversFrom);
    var end = Pkt.StartOf(batch.CoversTo.AddDays(1));
    var settled = context.SettlementLines.Select(l => l.TransactionRef);
    var unsettled = await context.Payments.AsNoTracking()
      .Where(p => p.Channel == batch.Channel && p.Status == PaymentStatus.Posted && p.PaidAt >= start && p.PaidAt < end && p.TransactionRef != null && !settled.Contains(p.TransactionRef))
      .OrderBy(p => p.PaidAt).Select(p => new UnsettledPaymentDto(p.Id.Value, p.TransactionRef, p.Psid, p.Amount, p.PaidAt)).ToListAsync(cancellationToken);

    var lines = batch.Lines.OrderBy(l => l.Match == SettlementMatch.Matched).ThenBy(l => l.TransactionRef)
      .Select(l => new SettlementLineDto(l.Id.Value, l.TransactionRef, l.Psid, l.Amount, l.PaidAt, l.Match, l.PaymentId?.Value, l.Note)).ToList();
    return Result<GetSettlementQueryResult>.Success(new(Dto(batch), lines, unsettled));
  }

  private static SettlementBatchDto Dto(SettlementBatch b) => new(b.Id.Value, b.Channel, b.BatchRef, b.SettlementDate, b.CoversFrom, b.CoversTo, b.ImportedAt, b.TotalAmount,
    b.LineCount, b.MatchedCount, b.ExceptionCount);
}
