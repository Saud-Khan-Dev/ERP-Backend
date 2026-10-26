/// One line of a module's audit trail as Administration reads it: who did what, to which record, when and from where,
/// with the value before and after when one fact changed. Every service answers `GET /audit` with the same shape, so
/// the Activity screen lists all modules in one diary.
///
/// One line per changed fact (a save that changed three fields is three lines, same `At`, same actor, same RequestId).
/// Action: <see cref="AuditActions"/> - Added, Changed, Removed, Exported, Printed, Ran - or the record's own verb where
/// the module keeps one ("Approved", "Reversed"). RecordKind: the kind of record in words ("Lease", "Bank account").
/// RecordCode: its business code (LSE-00012, EMP-004); RecordLabel: its name ("Ayesha Noor", a holiday's name).
/// Parent: the record it hangs off (the property, employee, tax file or asset) - a history tab lists by it.
/// Field: the fact in words ("Lease end date"); OldValue / NewValue as a person reads them (names, never ids).
/// Detail: anything else worth keeping, already human-readable ("120 rows · status = active").
/// ActorId null with ActorName "System · ..." is the system itself (a scheduled job, startup), never a person.
public sealed record AuditEntryDto(
  Guid Id,
  DateTimeOffset At,
  string Module,
  Guid? ActorId,
  string ActorName,
  string Action,
  string RecordKind,
  Guid? RecordId,
  string? RecordCode,
  string? RecordLabel,
  Guid? ParentId,
  string? ParentCode,
  string? Field,
  string? OldValue,
  string? NewValue,
  string? Detail,
  string? IpAddress,
  string? Device,
  string? RequestId);

/// The actions every module uses; a module may add its own verb where the record keeps one.
public static class AuditActions
{
  public const string Added = "Added";
  public const string Changed = "Changed";
  public const string Removed = "Removed";
  /// A register or report left the system as a file.
  public const string Exported = "Exported";
  /// A notice, certificate, slip or report was printed.
  public const string Printed = "Printed";
  /// A document or file was downloaded (saved to the person's computer).
  public const string Downloaded = "Downloaded";
  /// A document or file was opened on screen in the viewer.
  public const string Viewed = "Viewed";
  /// A run over many records at once (the daily job, a depreciation run, closing a day).
  public const string Ran = "Ran";
  /// A refused attempt worth keeping (an act blocked by a duty rule).
  public const string Refused = "Refused";
}

/// `GET /audit`: newest first. `before` continues a listing (strictly older than the given moment); `from` / `to`
/// bound the days (inclusive, Pakistan time); `actorId` one person; `action` one of the actions; `kind` one record kind;
/// `recordId` / `parentId` one record or everything under it; `search` matches the record kind, code, label, field,
/// values, detail and the person; `pageSize` 1-200 (default 50). Count is every line the filters match (ignoring `before`).
public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Data, long Count);

public sealed record AuditResponse(AuditPageDto Audit);

/// `GET /audit/kinds`: the record kinds and actions the module's trail holds, for the filters.
public sealed record AuditKindsResponse(IReadOnlyList<string> Kinds, IReadOnlyList<string> Actions);

/// `POST /audit/exports` (any of the module's EXPORT permissions): the screen tells the service a file was made or a page
/// printed, so the trail says who took what out. What: the register or report in words ("Employees register"); Rows:
/// lines in the file; Filters: the filters in words ("status = active, unit = Finance"); Format: "csv" (default),
/// "print" or "download". RecordId / RecordCode: the one record it was about (a payslip, a clearance), when there is one.
public sealed record RecordExportRequest(string What, int Rows, string? Filters, string? Format, Guid? RecordId = null, string? RecordCode = null);
