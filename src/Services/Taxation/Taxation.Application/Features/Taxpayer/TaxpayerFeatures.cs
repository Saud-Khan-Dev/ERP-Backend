using MediatR;
using Microsoft.EntityFrameworkCore;

/// The taxpayer portal: a person whose account is linked to their Property owner record (Identity, `own` claim) reads their
/// own properties, demands, notices, payments and messages, and how to pay. Read only. Anything that is not theirs is
/// "not found", as if it did not exist.
public sealed record GetMyTaxesQuery : IQuery<Result<MyTaxesDto>>;
public sealed record GetMyTaxFileQuery(Guid Id) : IQuery<Result<MyTaxFileDetailDto>>;
public sealed record GetMyDemandQuery(Guid Id) : IQuery<Result<GetDemandQueryResult>>;
public sealed record GetMyNoticeQuery(Guid Id) : IQuery<Result<GetNoticeQueryResult>>;
public sealed record GetMyPaymentDetailsQuery(Guid TaxFileId) : IQuery<Result<MyPaymentDetailsDto>>;
public sealed record GetMyMessagesQueryResult(PaginatedResult<MyMessageDto> Messages);
public sealed record GetMyMessagesQuery(PaginationRequest Pagination) : IQuery<Result<GetMyMessagesQueryResult>>;

public class TaxpayerHandlers(IApplicationDbContext context, ICurrentUser currentUser, Ledger ledger, TaxSetup setup, ISender sender, IClock clock) :
  IQueryHandler<GetMyTaxesQuery, Result<MyTaxesDto>>,
  IQueryHandler<GetMyTaxFileQuery, Result<MyTaxFileDetailDto>>,
  IQueryHandler<GetMyDemandQuery, Result<GetDemandQueryResult>>,
  IQueryHandler<GetMyNoticeQuery, Result<GetNoticeQueryResult>>,
  IQueryHandler<GetMyPaymentDetailsQuery, Result<MyPaymentDetailsDto>>,
  IQueryHandler<GetMyMessagesQuery, Result<GetMyMessagesQueryResult>>
{
  public async Task<Result<MyTaxesDto>> Handle(GetMyTaxesQuery query, CancellationToken cancellationToken)
  {
    if (currentUser.OwnerId is not { } owner)
      return Result<MyTaxesDto>.Success(new(false, null, 0, []));

    var today = clock.Today;
    var fileIds = await context.MyFileIds(owner, today).ToListAsync(cancellationToken);
    var files = new List<MyTaxFileDto>();
    foreach (var id in fileIds)
      files.Add(await FileDtoAsync(await context.LoadTaxFileAsync(id.Value, cancellationToken), owner, today, cancellationToken));
    var name = await context.TaxFilePayers.AsNoTracking().Where(p => p.OwnerId == owner).OrderByDescending(p => p.SyncedAt).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
      ?? currentUser.DisplayName;
    return Result<MyTaxesDto>.Success(new(true, name, files.Sum(f => f.Dues), files.OrderByDescending(f => f.Dues).ThenBy(f => f.FileNo).ToList()));
  }

  public async Task<Result<MyTaxFileDetailDto>> Handle(GetMyTaxFileQuery query, CancellationToken cancellationToken)
  {
    var (owner, file) = await MyFileAsync(query.Id, cancellationToken);
    var today = clock.Today;
    var dto = await FileDtoAsync(file, owner, today, cancellationToken);

    var demands = (await ledger.DemandsAsync(file.Id, cancellationToken)).Where(d => d.ShownToTaxpayer()).OrderByDescending(d => d.FiscalYear).ToList();
    var demandIds = demands.Select(d => d.Id).ToList();
    var notices = await context.Notices.AsNoTracking().Where(n => demandIds.Contains(n.DemandId) && n.Status == NoticeStatus.Issued)
      .OrderByDescending(n => n.IssueDate).ThenByDescending(n => n.NoticeNo).ToListAsync(cancellationToken);
    var years = demands.ToDictionary(d => d.Id, d => d.FiscalYear);
    var payments = await context.Payments.AsNoTracking().Include(p => p.Allocations).Where(p => p.TaxFileId == file.Id)
      .OrderByDescending(p => p.PaidAt).ToListAsync(cancellationToken);

    return Result<MyTaxFileDetailDto>.Success(new(dto,
      demands.Select(d => d.ToSummary(file.FileNo, file.Psid)).ToList(),
      notices.Select(n => new MyNoticeDto(n.Id.Value, n.NoticeNo, n.EndstNo, n.DemandId.Value, years[n.DemandId], FiscalYear.Label(years[n.DemandId]), n.Stage, n.IssueDate,
        n.ReplyBy, n.AmountWithinDue, n.ServedOn)).ToList(),
      await PaymentDtos.ForAsync(context, setup, payments, cancellationToken)));
  }

  public async Task<Result<GetDemandQueryResult>> Handle(GetMyDemandQuery query, CancellationToken cancellationToken)
  {
    var owner = Owner();
    var id = DemandId.Of(query.Id);
    var demand = await context.Demands.AsNoTracking().Where(d => d.Id == id).Select(d => new { d.TaxFileId, d.Status }).FirstOrDefaultAsync(cancellationToken);
    if (demand is null || demand.Status is DemandStatus.Draft or DemandStatus.Cancelled || !await OwnsAsync(owner, demand.TaxFileId, cancellationToken))
      throw new DemandNotFoundException($"Demand {query.Id} was not found.");
    var result = await sender.Send(new GetDemandQuery(query.Id), cancellationToken);
    // only what was issued to them: no notice still waiting for the Section's confirmation, none cancelled
    return result.IsSuccess
      ? Result<GetDemandQueryResult>.Success(new(result.Value!.Demand with { Notices = result.Value.Demand.Notices.Where(n => n.Status == NoticeStatus.Issued).ToList() }))
      : result;
  }

  public async Task<Result<GetNoticeQueryResult>> Handle(GetMyNoticeQuery query, CancellationToken cancellationToken)
  {
    var owner = Owner();
    var id = NoticeId.Of(query.Id);
    var notice = await context.Notices.AsNoTracking().Where(n => n.Id == id).Select(n => new { n.TaxFileId, n.Status }).FirstOrDefaultAsync(cancellationToken);
    if (notice is null || notice.Status != NoticeStatus.Issued || !await OwnsAsync(owner, notice.TaxFileId, cancellationToken))
      throw new NoticeNotFoundException($"Notice {query.Id} was not found.");
    return await sender.Send(new GetNoticeQuery(query.Id), cancellationToken);
  }

  public async Task<Result<MyPaymentDetailsDto>> Handle(GetMyPaymentDetailsQuery query, CancellationToken cancellationToken)
  {
    var (_, file) = await MyFileAsync(query.TaxFileId, cancellationToken);
    if (file.Psid is null)
      return Result<MyPaymentDetailsDto>.Failure($"{file.FileNo} has no 1Link reference yet.");
    var today = clock.Today;
    var dues = await ledger.DuesAsync(file, cancellationToken);
    var settings = await setup.SettingsAsync(today, cancellationToken);
    var letterhead = await context.Letterheads.AsNoTracking().Where(l => l.EffectiveFrom <= today && (l.EffectiveTo == null || l.EffectiveTo >= today))
      .OrderByDescending(l => l.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);
    return Result<MyPaymentDetailsDto>.Success(new(file.Id.Value, file.FileNo, file.Psid, Numbers.FormatPsid(file.Psid), dues, file.Addressee(today)?.Name,
      settings.QrMerchantName, letterhead?.QrBankName, dues > 0 ? PaymentQr.Payload(settings, file.Psid, dues, file.FileNo) : null));
  }

  public async Task<Result<GetMyMessagesQueryResult>> Handle(GetMyMessagesQuery query, CancellationToken cancellationToken)
  {
    var owner = Owner();
    var rows = context.SmsMessages.AsNoTracking().Where(m => m.OwnerId == owner);
    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(m => m.QueuedAt).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);
    var fileIds = page.Where(m => m.TaxFileId is not null).Select(m => m.TaxFileId!).Distinct().ToList();
    var fileNos = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var data = page.Select(m => new MyMessageDto(m.Id.Value, m.TaxFileId is { } f ? fileNos.GetValueOrDefault(f) : null, m.Body, m.QueuedAt, m.SentAt, m.DeliveryStatus)).ToList();
    return Result<GetMyMessagesQueryResult>.Success(new(new PaginatedResult<MyMessageDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  private Guid Owner() => currentUser.OwnerId ?? throw new TaxFileNotFoundException("This account is not registered as a taxpayer.");

  private async Task<bool> OwnsAsync(Guid owner, TaxFileId fileId, CancellationToken cancellationToken) =>
      await context.MyFileIds(owner, clock.Today).AnyAsync(id => id == fileId, cancellationToken);

  private async Task<(Guid Owner, TaxFile File)> MyFileAsync(Guid id, CancellationToken cancellationToken)
  {
    var owner = Owner();
    var fileId = TaxFileId.Of(id);
    if (!await OwnsAsync(owner, fileId, cancellationToken))
      throw new TaxFileNotFoundException($"Tax file {id} was not found.");
    return (owner, await context.LoadTaxFileAsync(id, cancellationToken));
  }

  private async Task<MyTaxFileDto> FileDtoAsync(TaxFile file, Guid owner, DateOnly today, CancellationToken cancellationToken)
  {
    var payer = file.Payers.Where(p => p.OwnerId == owner && p.Covers(today)).OrderByDescending(p => p.SharePct).First();
    var town = await context.CircleTowns.AsNoTracking().Where(t => t.TownId == file.TownId && t.EffectiveFrom <= today && (t.EffectiveTo == null || t.EffectiveTo >= today))
      .Select(t => new { t.ShortCode, t.TownCode }).FirstOrDefaultAsync(cancellationToken);
    var current = (await ledger.DemandsAsync(file.Id, cancellationToken)).Where(d => d.ShownToTaxpayer()).OrderByDescending(d => d.FiscalYear).FirstOrDefault();
    return new MyTaxFileDto(file.Id.Value, file.FileNo, file.PropertyCode, file.PlotNo, file.OldReferenceNo, town?.ShortCode, town?.TownCode, file.TaxCategory, file.TaxStatus,
      payer.PayerRole, payer.SharePct, file.Psid, file.Psid is null ? null : Numbers.FormatPsid(file.Psid), await ledger.DuesAsync(file, cancellationToken),
      current?.ToSummary(file.FileNo, file.Psid));
  }
}
