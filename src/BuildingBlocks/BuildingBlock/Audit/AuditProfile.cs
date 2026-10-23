using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

/// How one service's records read in its audit trail. Every entity the service saves is recorded by default - its kind is
/// the class name in words, its code and name are found by the usual property names (Code, Number, Name, Title ...),
/// its fields are their names in words. A service refines what reads badly: the kind's words, the parent a record hangs
/// off (so the property's or the employee's history lists it), a field's words, fields to leave out, and the bulk rows a
/// run writes (skipped; the run records one "Ran" line instead).
public sealed partial class AuditProfile(string module)
{
  /// The module's name in the trail ("Taxation", "Human Resources").
  public string Module { get; } = module;

  private readonly Dictionary<Type, IAuditEntityConfig> _configs = [];
  private readonly HashSet<Type> _skipped = [];

  /// Never written, on any record (bookkeeping columns, secrets).
  private readonly HashSet<string> _ignored = new(StringComparer.Ordinal)
  {
    "Id", "CreatedAt", "CreatedBy", "CreatedByName", "UpdatedAt", "UpdatedBy", "UpdatedByName", "LastModified", "LastModifiedBy",
    "LastModifiedByName", "ModifiedAt", "ModifiedBy", "Version", "RowVersion", "ConcurrencyStamp", "xmin", "PasswordHash", "TokenHash",
    "RefreshTokenHash", "SecurityStamp", "DomainEvents"
  };

  /// Describe one entity type.
  public AuditProfile Entity<T>(Action<AuditEntityConfig<T>> configure) where T : class
  {
    var config = new AuditEntityConfig<T>();
    configure(config);
    _configs[typeof(T)] = config;
    return this;
  }

  /// Rows a run writes in bulk (demand lines, payslip lines): not recorded one by one.
  public AuditProfile Skip<T>() where T : class
  {
    _skipped.Add(typeof(T));
    return this;
  }

  /// Fields left out on every record (by property name).
  public AuditProfile Ignore(params string[] fieldNames)
  {
    foreach (var name in fieldNames)
      _ignored.Add(name);
    return this;
  }

  internal bool IsSkipped(Type type) => type == typeof(AuditRecord) || _skipped.Contains(type) || (type.BaseType is { } b && b != typeof(object) && _skipped.Contains(b));

  internal IAuditEntityConfig? ConfigOf(Type type) => _configs.TryGetValue(type, out var config) ? config : null;

  internal bool IsIgnored(Type type, string field) => _ignored.Contains(field) || (ConfigOf(type)?.Ignored.Contains(field) ?? false);

  /// "TaxFileOverride" → "Tax file override".
  public string KindOf(Type type) => ConfigOf(type)?.Kind ?? Words(type.Name);

  public string FieldOf(Type type, string property) =>
    ConfigOf(type)?.Fields.TryGetValue(property, out var label) == true ? label! : Words(property.EndsWith("Id", StringComparison.Ordinal) && property.Length > 2 ? property[..^2] : property);

  /// The record's business code and its name, as found on it.
  public (string? Code, string? Label) Describe(object entity)
  {
    var type = entity.GetType();
    var config = ConfigOf(type);
    var code = config?.CodeOf(entity) ?? FirstText(entity, CodeNames);
    var label = config?.LabelOf(entity) ?? FirstText(entity, LabelNames);
    return (code, label == code ? null : label);
  }

  /// "EMP-004 · Ayesha Noor", or whichever half there is.
  public string? Name(object entity)
  {
    var (code, label) = Describe(entity);
    return code is null ? label : label is null ? code : $"{code} · {label}";
  }

  internal (Type Type, object? Key)? ParentOf(object entity) => ConfigOf(entity.GetType())?.ParentOf(entity);

  private static readonly string[] CodeNames = ["Code", "Number", "EmployeeNumber", "PropertyCode", "AssetCode", "AssetTag", "FileNumber", "ReferenceNumber", "Reference", "CaseNumber", "NoticeNumber", "ReceiptNumber", "CertificateNumber"];
  private static readonly string[] LabelNames = ["Name", "FullName", "DisplayName", "Title", "Label", "Description"];

  private static string? FirstText(object entity, string[] names)
  {
    foreach (var name in names)
    {
      var property = entity.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
      if (property is null)
        continue;
      var text = AuditValues.Text(property.GetValue(entity));
      if (!string.IsNullOrWhiteSpace(text))
        return text;
    }
    return null;
  }

  /// "BasicPayAmount" → "Basic pay amount"; "CNIC" stays "CNIC".
  public static string Words(string name)
  {
    var spaced = SplitWords().Replace(name, " $1").Trim();
    var parts = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parts.Select((p, i) => p.All(char.IsUpper) && p.Length > 1 ? p : i == 0 ? p : p.ToLower(CultureInfo.InvariantCulture)));
  }

  [GeneratedRegex("(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])")]
  private static partial Regex SplitWords();
}

internal interface IAuditEntityConfig
{
  string? Kind { get; }
  IReadOnlyDictionary<string, string> Fields { get; }
  ISet<string> Ignored { get; }
  string? CodeOf(object entity);
  string? LabelOf(object entity);
  (Type Type, object? Key)? ParentOf(object entity);
}

public sealed class AuditEntityConfig<T> : IAuditEntityConfig where T : class
{
  private string? _kind;
  private Func<T, string?>? _code;
  private Func<T, string?>? _label;
  private (Type Type, Func<T, object?> Key)? _parent;
  private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);
  private readonly HashSet<string> _ignored = new(StringComparer.Ordinal);

  /// The kind in words ("Lease", "Bank account").
  public AuditEntityConfig<T> Kind(string kind) { _kind = kind; return this; }

  /// The business code (LSE-00012).
  public AuditEntityConfig<T> Code(Func<T, string?> code) { _code = code; return this; }

  /// The name (a person's name, a holiday's name).
  public AuditEntityConfig<T> Label(Func<T, string?> label) { _label = label; return this; }

  /// The record this one hangs off (by its key on this record): the parent's history lists this record's changes.
  public AuditEntityConfig<T> Under<TParent>(Func<T, object?> parentKey) where TParent : class { _parent = (typeof(TParent), parentKey); return this; }

  /// A field's words ("EndDate" → "Lease end date").
  public AuditEntityConfig<T> Field(string property, string label) { _fields[property] = label; return this; }

  /// Fields never written for this record.
  public AuditEntityConfig<T> Ignore(params string[] properties) { foreach (var p in properties) _ignored.Add(p); return this; }

  string? IAuditEntityConfig.Kind => _kind;
  IReadOnlyDictionary<string, string> IAuditEntityConfig.Fields => _fields;
  ISet<string> IAuditEntityConfig.Ignored => _ignored;
  string? IAuditEntityConfig.CodeOf(object entity) => _code?.Invoke((T)entity);
  string? IAuditEntityConfig.LabelOf(object entity) => _label?.Invoke((T)entity);
  (Type Type, object? Key)? IAuditEntityConfig.ParentOf(object entity) => _parent is { } p ? (p.Type, p.Key((T)entity)) : null;
}

/// Values as a person reads them.
public static class AuditValues
{
  /// A typed id (`PropertyId { Value }`) and any single-value object (`Name { Value }`) as its value; enums in words;
  /// yes/no; dates as dates; amounts as written; anything else as text.
  public static string? Text(object? value) => value switch
  {
    null => null,
    string s => s,
    bool b => b ? "Yes" : "No",
    Enum e => AuditProfile.Words(e.ToString()),
    DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
    DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
    TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
    decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
    double or float or int or long or short or byte or uint or ulong => Convert.ToString(value, CultureInfo.InvariantCulture),
    Guid g => g.ToString(),
    System.Collections.IEnumerable list => string.Join(", ", list.Cast<object?>().Select(Text).Where(x => x is not null)),
    _ => SingleValue(value) ?? value.ToString()
  };

  /// The Guid inside a typed id, or the Guid itself; null for anything else.
  public static Guid? Guid(object? value) => value switch
  {
    null => null,
    System.Guid g => g,
    _ => value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) is System.Guid inner ? inner : null
  };

  private static string? SingleValue(object value)
  {
    var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
    var single = properties.FirstOrDefault(p => p.Name == "Value") ?? (properties.Length == 1 ? properties[0] : null);
    return single is null ? null : Text(single.GetValue(value));
  }
}
