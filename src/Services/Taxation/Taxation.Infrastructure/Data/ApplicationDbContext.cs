using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; this service owns the "taxation" schema.
  public const string Schema = "taxation";

  /// The database sequences behind the running numbers (INumberSource).
  public static readonly IReadOnlyDictionary<NumberSequence, string> Sequences = new Dictionary<NumberSequence, string>
  {
    [NumberSequence.Psid] = "psid_seq",
    [NumberSequence.NoticeRef] = "notice_ref_seq",
    [NumberSequence.DemandNo] = "demand_no_seq",
    [NumberSequence.FeeNo] = "fee_no_seq",
    [NumberSequence.ClearanceNo] = "clearance_no_seq",
    [NumberSequence.ReceiptNo] = "receipt_no_seq"
  };

  private readonly ICurrentUser? _currentUser;

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser? currentUser = null) : base(options)
  {
    _currentUser = currentUser;
  }

  /// The towns the signed-in person works on (Administration › a person's scope); null = every town. Read once per
  /// request; a background job has no person and sees every town.
  public List<Guid>? TownScope
  {
    get
    {
      if (!_scopeRead)
      {
        _townScope = _currentUser?.TownScope?.ToList();
        _scopeRead = true;
      }
      return _townScope;
    }
  }
  private List<Guid>? _townScope;
  private bool _scopeRead;

  // ---- 1. organisation ----
  public DbSet<TaxCircle> TaxCircles => Set<TaxCircle>();
  public DbSet<TaxCircleTown> CircleTowns => Set<TaxCircleTown>();
  public DbSet<TaxCircleStaff> CircleStaff => Set<TaxCircleStaff>();

  // ---- 2. rate masters and settings ----
  public DbSet<TaxHead> TaxHeads => Set<TaxHead>();
  public DbSet<CategoryMap> CategoryMaps => Set<CategoryMap>();
  public DbSet<RateSet> RateSets => Set<RateSet>();
  public DbSet<LtfFactor> LtfFactors => Set<LtfFactor>();
  public DbSet<RateBand> RateBands => Set<RateBand>();
  public DbSet<TaxSetting> Settings => Set<TaxSetting>();
  public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
  public DbSet<SpecialPolicy> SpecialPolicies => Set<SpecialPolicy>();
  public DbSet<PenaltyRule> PenaltyRules => Set<PenaltyRule>();
  public DbSet<NoticeTemplate> NoticeTemplates => Set<NoticeTemplate>();
  public DbSet<NoticeLetterhead> Letterheads => Set<NoticeLetterhead>();
  public DbSet<NoticeTableLayout> TableLayouts => Set<NoticeTableLayout>();

  // ---- 3. tax files ----
  public DbSet<TaxFile> TaxFiles => Set<TaxFile>();
  public DbSet<TaxFileBasis> TaxFileBases => Set<TaxFileBasis>();
  public DbSet<TaxFilePayer> TaxFilePayers => Set<TaxFilePayer>();
  public DbSet<TaxFileHeadValue> HeadValues => Set<TaxFileHeadValue>();
  public DbSet<TaxFileOverride> Overrides => Set<TaxFileOverride>();
  public DbSet<LitigationHold> Holds => Set<LitigationHold>();
  public DbSet<AgreementSchedule> Agreements => Set<AgreementSchedule>();

  // ---- 4. demand, notices, penalties ----
  public DbSet<DemandRun> DemandRuns => Set<DemandRun>();
  public DbSet<Demand> Demands => Set<Demand>();
  public DbSet<DemandLine> DemandLines => Set<DemandLine>();
  public DbSet<Arrears> ArrearsRows => Set<Arrears>();
  public DbSet<Notice> Notices => Set<Notice>();
  public DbSet<Penalty> Penalties => Set<Penalty>();
  public DbSet<PenaltyRun> PenaltyRuns => Set<PenaltyRun>();

  // ---- 5. reference, payments ----
  public DbSet<PsidSync> PsidSyncs => Set<PsidSync>();
  public DbSet<Payment> Payments => Set<Payment>();
  public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

  // ---- 6. fees, parties, clearances, messages ----
  public DbSet<Party> Parties => Set<Party>();
  public DbSet<Fee> Fees => Set<Fee>();
  public DbSet<Clearance> Clearances => Set<Clearance>();
  public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

  // ---- 7. the daily job and the change history ----
  public DbSet<JobRun> JobRuns => Set<JobRun>();
  /// The change history kept until 2026-10-21 (read-only now; its rows were copied into audit_entry).
  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
  /// The audit trail (BuildingBlock/Audit): append-only, written by AuditTrailInterceptor and IAuditTrail.
  public DbSet<AuditRecord> AuditTrail => Set<AuditRecord>();

  public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
      Database.BeginTransactionAsync(cancellationToken);

  public async Task LockTaxFileAsync(TaxFileId id, CancellationToken cancellationToken)
  {
    if (Database.CurrentTransaction is null)
      throw new InvalidOperationException("A tax file is locked only inside a transaction.");
    var fileId = id.Value;
    await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM taxation.tax_file WHERE id = {fileId} FOR UPDATE", cancellationToken);
    await Database.ExecuteSqlInterpolatedAsync(
      $"SELECT 1 FROM taxation.demand WHERE tax_file_id = {fileId} AND status IN ('ISSUED', 'PART_PAID', 'DEFAULTED', 'PAID') FOR UPDATE", cancellationToken);
  }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    // every strongly typed id is a uuid column
    var idTypes = typeof(TaxFileId).Assembly.GetTypes()
      .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>)));
    foreach (var idType in idTypes)
      configurationBuilder.Properties(idType).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(idType));

    // the DBML's strings are varchar(n) with their stated lengths (set in the configurations), text where it says text;
    // money is numeric(18,2)
    configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

    // besides the indexes the DBML lists, every foreign key gets EF's index (ix_<table>_<column>): notices by demand,
    // allocations by line and the like are read on every payment and print
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    builder.HasDefaultSchema(Schema);
    builder.AddAuditTrail();
    foreach (var sequence in Sequences.Values)
      builder.HasSequence<long>(sequence).StartsAt(1).IncrementsBy(1);

    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    foreach (var entity in builder.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() is not null && !e.IsOwned()))
    {
      var table = entity.GetTableName()!;

      // Postgres' own names: <table>_pkey and <table>_<columns>_fkey, unless a configuration named it
      entity.FindPrimaryKey()!.SetName($"{table}_pkey");
      foreach (var foreignKey in entity.GetForeignKeys())
      {
        if (((IConventionForeignKey)foreignKey).GetConstraintNameConfigurationSource() != ConfigurationSource.Explicit)
          foreignKey.SetConstraintName(Truncate($"{table}_{string.Join("_", foreignKey.Properties.Select(p => p.GetColumnName()))}_fkey"));
      }

      // The domain always sets every value, so EF always sends it; the database defaults are there for SQL written by
      // hand. Without this EF would leave out a false / 0 / first-enum value and let the default replace it.
      foreach (var property in entity.GetProperties())
      {
        if (property.GetComputedColumnSql() is null && (property.GetDefaultValueSql() is not null || property.TryGetDefaultValue(out _)))
          property.ValueGenerated = ValueGenerated.Never;
      }
    }

    base.OnModelCreating(builder);
  }

  /// Postgres cuts identifiers at 63 bytes.
  private static string Truncate(string name) => name.Length <= 63 ? name : name[..63];
}
