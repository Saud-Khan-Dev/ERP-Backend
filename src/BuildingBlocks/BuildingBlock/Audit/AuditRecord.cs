using Microsoft.EntityFrameworkCore;

/// A stored audit line (table `audit_entry` in the service's own schema). Written only by the audit trail - the
/// interceptor and <see cref="IAuditTrail"/> - and never changed or deleted: the table refuses UPDATE, DELETE and
/// TRUNCATE (<see cref="AuditTrailSql"/>). Kept forever; GDA's records are government records.
public sealed class AuditRecord
{
  public Guid Id { get; private set; }
  public DateTimeOffset At { get; private set; }
  public string Module { get; private set; } = default!;
  public Guid? ActorId { get; private set; }
  public string ActorName { get; private set; } = default!;
  public string Action { get; private set; } = default!;
  public string RecordKind { get; private set; } = default!;
  public Guid? RecordId { get; private set; }
  public string? RecordCode { get; private set; }
  public string? RecordLabel { get; private set; }
  public Guid? ParentId { get; private set; }
  public string? ParentCode { get; private set; }
  public string? Field { get; private set; }
  public string? OldValue { get; private set; }
  public string? NewValue { get; private set; }
  public string? Detail { get; private set; }
  public string? IpAddress { get; private set; }
  public string? Device { get; private set; }
  public string? RequestId { get; private set; }

  private AuditRecord() { }

  public static AuditRecord Create(DateTimeOffset at, string module, Guid? actorId, string actorName, string action, string recordKind,
      Guid? recordId, string? recordCode, string? recordLabel, Guid? parentId, string? parentCode, string? field, string? oldValue,
      string? newValue, string? detail, string? ipAddress, string? device, string? requestId) => new()
  {
    Id = Guid.CreateVersion7(),
    At = at,
    Module = Cut(module, 60)!,
    ActorId = actorId,
    ActorName = Cut(string.IsNullOrWhiteSpace(actorName) ? "System" : actorName, 200)!,
    Action = Cut(action, 40)!,
    RecordKind = Cut(recordKind, 100)!,
    RecordId = recordId,
    RecordCode = Cut(recordCode, 100),
    RecordLabel = Cut(recordLabel, 300),
    ParentId = parentId,
    ParentCode = Cut(parentCode, 100),
    Field = Cut(field, 150),
    OldValue = Cut(oldValue, 4000),
    NewValue = Cut(newValue, 4000),
    Detail = Cut(detail, 8000),
    IpAddress = Cut(ipAddress, 64),
    Device = Cut(device, 300),
    RequestId = Cut(requestId, 100),
  };

  public AuditEntryDto ToDto() => new(Id, At, Module, ActorId, ActorName, Action, RecordKind, RecordId, RecordCode, RecordLabel, ParentId,
    ParentCode, Field, OldValue, NewValue, Detail, IpAddress, Device, RequestId);

  private static string? Cut(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..(max - 1)] + "…";
}

public static class AuditModelBuilderExtensions
{
  /// Maps the audit trail's table into the service's model (call from OnModelCreating, after HasDefaultSchema).
  public static ModelBuilder AddAuditTrail(this ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<AuditRecord>(b =>
    {
      b.ToTable("audit_entry");
      b.HasKey(x => x.Id);
      b.Property(x => x.Module).HasMaxLength(60).IsRequired();
      b.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
      b.Property(x => x.Action).HasMaxLength(40).IsRequired();
      b.Property(x => x.RecordKind).HasMaxLength(100).IsRequired();
      b.Property(x => x.RecordCode).HasMaxLength(100);
      b.Property(x => x.RecordLabel).HasMaxLength(300);
      b.Property(x => x.ParentCode).HasMaxLength(100);
      b.Property(x => x.Field).HasMaxLength(150);
      b.Property(x => x.OldValue).HasMaxLength(4000);
      b.Property(x => x.NewValue).HasMaxLength(4000);
      b.Property(x => x.IpAddress).HasMaxLength(64);
      b.Property(x => x.Device).HasMaxLength(300);
      b.Property(x => x.RequestId).HasMaxLength(100);
      b.HasIndex(x => x.At);
      b.HasIndex(x => new { x.ParentId, x.At });
      b.HasIndex(x => new { x.RecordId, x.At });
      b.HasIndex(x => new { x.ActorId, x.At });
      b.HasIndex(x => new { x.RecordKind, x.At });
    });
    return modelBuilder;
  }
}

/// SQL for the migration that creates `audit_entry`: after it, nothing - no API, no handler, no one at a psql prompt
/// short of a superuser dropping the trigger - can change or delete a line. Call `Protect` at the end of Up and
/// `Unprotect` at the start of Down.
public static class AuditTrailSql
{
  public static string Protect(string schema) => $"""
    CREATE OR REPLACE FUNCTION "{schema}".audit_entry_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      RAISE EXCEPTION 'The audit trail is append-only: its lines cannot be changed or deleted.';
    END $$;
    CREATE TRIGGER audit_entry_no_change BEFORE UPDATE OR DELETE ON "{schema}".audit_entry
      FOR EACH ROW EXECUTE FUNCTION "{schema}".audit_entry_append_only();
    CREATE TRIGGER audit_entry_no_truncate BEFORE TRUNCATE ON "{schema}".audit_entry
      FOR EACH STATEMENT EXECUTE FUNCTION "{schema}".audit_entry_append_only();
    """;

  public static string Unprotect(string schema) => $"""
    DROP TRIGGER IF EXISTS audit_entry_no_change ON "{schema}".audit_entry;
    DROP TRIGGER IF EXISTS audit_entry_no_truncate ON "{schema}".audit_entry;
    DROP FUNCTION IF EXISTS "{schema}".audit_entry_append_only();
    """;
}
