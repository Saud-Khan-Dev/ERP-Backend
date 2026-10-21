using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// Writes taxation.audit_log in the same save as the change: who changed what and when, with the values before and after
/// (only the fields that changed). Kept for the configuration and for the decisions on files and money; the rows a run
/// produces in bulk (demands, their lines, notices, carried arrears, reference syncs, SMS) are not logged here - they
/// carry their own who and when.
public sealed class AuditLogInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
  private static readonly HashSet<Type> Logged =
  [
    typeof(TaxCircle), typeof(TaxCircleTown), typeof(TaxCircleStaff), typeof(TaxHead), typeof(CategoryMap), typeof(RateSet), typeof(TaxSetting),
    typeof(MessageTemplate), typeof(SpecialPolicy), typeof(PenaltyRule), typeof(NoticeTemplate), typeof(NoticeLetterhead), typeof(NoticeTableLayout),
    typeof(TaxFile), typeof(TaxFileOverride), typeof(LitigationHold), typeof(TaxFileHeadValue), typeof(AgreementSchedule), typeof(AgreementStepRevision),
    typeof(Arrears), typeof(Fee), typeof(Clearance), typeof(Payment), typeof(Party), typeof(DemandRun), typeof(PenaltyRun)
  ];

  private static readonly HashSet<string> Skipped = ["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Version", "Id"];

  private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
  {
    Record(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
  {
    Record(eventData.Context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void Record(DbContext? context)
  {
    if (context is null)
      return;
    context.ChangeTracker.DetectChanges();
    var now = DateTime.UtcNow;
    var rows = new List<AuditLog>();
    foreach (var entry in context.ChangeTracker.Entries())
    {
      if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) || !Logged.Contains(entry.Entity.GetType()))
        continue;
      // a balance carried by a run is the run's own work, not a decision
      if (entry.Entity is Arrears { Source: ArrearsSource.Demand })
        continue;

      var changed = entry.State == EntityState.Modified
        ? entry.Properties.Where(p => p.IsModified && !Skipped.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue)).ToList()
        : entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name)).ToList();
      if (entry.State == EntityState.Modified && changed.Count == 0)
        continue;

      string? Values(Func<PropertyEntry, object?> pick) =>
        JsonSerializer.Serialize(changed.ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Metadata.Name), p => Plain(pick(p))), Json);

      var id = entry.Property("Id").CurrentValue is ITypedId typed ? typed.Value : Guid.Empty;
      rows.Add(AuditLog.Record(
        entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
        id,
        entry.State switch { EntityState.Added => "ADDED", EntityState.Deleted => "DELETED", _ => "MODIFIED" },
        entry.State == EntityState.Added ? null : Values(p => p.OriginalValue),
        entry.State == EntityState.Deleted ? null : Values(p => p.CurrentValue),
        currentUser.UserId,
        currentUser.Username,
        now,
        FileOf(entry.Entity)));
    }
    if (rows.Count > 0)
      context.Set<AuditLog>().AddRange(rows);
  }

  /// The tax file a change belongs to (its history lists it).
  private static Guid? FileOf(object entity) => entity switch
  {
    TaxFile f => f.Id.Value,
    TaxFileOverride o => o.TaxFileId.Value,
    LitigationHold h => h.TaxFileId.Value,
    TaxFileHeadValue v => v.TaxFileId.Value,
    AgreementSchedule a => a.TaxFileId.Value,
    Arrears a => a.TaxFileId.Value,
    Fee f => f.TaxFileId?.Value,
    Clearance c => c.TaxFileId.Value,
    Payment p => p.TaxFileId?.Value,
    _ => null
  };

  /// Ids as their Guid, enums as their name, dates and amounts as they are; the rest as JSON would write it.
  private static object? Plain(object? value) => value switch
  {
    null => null,
    ITypedId id => id.Value,
    Enum e => e.ToString(),
    DateOnly or DateTime or decimal or int or long or bool or string or Guid or uint => value,
    _ => JsonSerializer.SerializeToElement(value, Json)
  };
}
