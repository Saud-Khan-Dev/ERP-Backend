using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public interface IApplicationDbContext
{
  // ---- 1. organisation ----
  DbSet<TaxCircle> TaxCircles { get; }
  DbSet<TaxCircleTown> CircleTowns { get; }
  DbSet<TaxCircleStaff> CircleStaff { get; }

  // ---- 2. rate masters and settings ----
  DbSet<TaxHead> TaxHeads { get; }
  DbSet<CategoryMap> CategoryMaps { get; }
  DbSet<RateSet> RateSets { get; }
  DbSet<LtfFactor> LtfFactors { get; }
  DbSet<RateBand> RateBands { get; }
  DbSet<TaxSetting> Settings { get; }
  DbSet<MessageTemplate> MessageTemplates { get; }
  DbSet<SpecialPolicy> SpecialPolicies { get; }
  DbSet<PenaltyRule> PenaltyRules { get; }
  DbSet<NoticeTemplate> NoticeTemplates { get; }
  DbSet<NoticeLetterhead> Letterheads { get; }
  DbSet<NoticeTableLayout> TableLayouts { get; }

  // ---- 3. tax files ----
  DbSet<TaxFile> TaxFiles { get; }
  DbSet<TaxFileBasis> TaxFileBases { get; }
  DbSet<TaxFilePayer> TaxFilePayers { get; }
  DbSet<TaxFileHeadValue> HeadValues { get; }
  DbSet<TaxFileOverride> Overrides { get; }
  DbSet<LitigationHold> Holds { get; }
  DbSet<AgreementSchedule> Agreements { get; }

  // ---- 4. demand, notices, penalties ----
  DbSet<DemandRun> DemandRuns { get; }
  DbSet<Demand> Demands { get; }
  DbSet<DemandLine> DemandLines { get; }
  DbSet<Arrears> ArrearsRows { get; }
  DbSet<Notice> Notices { get; }
  DbSet<Penalty> Penalties { get; }
  DbSet<PenaltyRun> PenaltyRuns { get; }

  // ---- 5. reference, payments ----
  DbSet<PsidSync> PsidSyncs { get; }
  DbSet<Payment> Payments { get; }
  DbSet<PaymentAllocation> PaymentAllocations { get; }

  // ---- 6. fees, parties, clearances, messages ----
  DbSet<Party> Parties { get; }
  DbSet<Fee> Fees { get; }
  DbSet<Clearance> Clearances { get; }
  DbSet<SmsMessage> SmsMessages { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);

  Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
