/// taxation.notice_template: the printed title and reminder paragraph of a notice stage. A saved change raises the
/// version; issued notices keep the version (and the paragraph) they were printed with.
public class NoticeTemplate : Aggregate<NoticeTemplateId>
{
  /// Placeholders a paragraph may use.
  public static readonly IReadOnlyList<string> Placeholders = ["amount", "days", "fy", "surcharge", "total", "reply_by", "psid", "code", "circle"];

  public NoticeStage Stage { get; private set; }
  public string Name { get; private set; } = default!;
  public string TitleText { get; private set; } = default!;
  public string ParagraphText { get; private set; } = default!;
  public string? SourceDocument { get; private set; }
  public int VersionNo { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public bool IsActive { get; private set; }

  private NoticeTemplate() { }

  public static NoticeTemplate Create(NoticeTemplateId id, NoticeStage stage, string name, string title, string paragraph, string? sourceDocument, DateOnly effectiveFrom) => new()
  {
    Id = id,
    Stage = stage,
    Name = Guard.RequiredText(name, 100, "Template name"),
    TitleText = Guard.RequiredText(title, 200, "Title"),
    ParagraphText = CheckParagraph(paragraph),
    SourceDocument = Guard.Text(sourceDocument, 255, "Source document"),
    VersionNo = 1,
    EffectiveFrom = effectiveFrom,
    IsActive = true
  };

  /// Returns true when the printed text changed (and the version went up).
  public bool Change(string name, string title, string paragraph, string? sourceDocument, DateOnly today)
  {
    Name = Guard.RequiredText(name, 100, "Template name");
    SourceDocument = Guard.Text(sourceDocument, 255, "Source document");
    var newTitle = Guard.RequiredText(title, 200, "Title");
    var newParagraph = CheckParagraph(paragraph);
    if (newTitle == TitleText && newParagraph == ParagraphText)
      return false;

    TitleText = newTitle;
    ParagraphText = newParagraph;
    VersionNo++;
    EffectiveFrom = today;
    return true;
  }

  /// A paragraph as printed: required, at most 4000 characters, only the known placeholders.
  public static string CheckParagraph(string paragraph)
  {
    var text = Guard.RequiredText(paragraph, 4000, "Paragraph");
    var unknown = System.Text.RegularExpressions.Regex.Matches(text, @"\{(\w+)\}").Select(m => m.Groups[1].Value)
      .Where(p => !Placeholders.Contains(p.ToLowerInvariant().Replace("replyby", "reply_by"))).Distinct().ToList();
    if (unknown.Count > 0)
      throw new DomainException($"Unknown placeholder(s) {string.Join(", ", unknown.Select(u => "{" + u + "}"))}. Use {string.Join(" ", Placeholders.Select(p => "{" + p + "}"))}.");
    return text;
  }
}

/// taxation.notice_letterhead: the common parts of every printed notice (government and authority lines, motto, notes,
/// Cc lines, QR text, logos, watermark). A change starts a new row from a date; notices keep the row they were printed with.
public class NoticeLetterhead : Aggregate<NoticeLetterheadId>
{
  private List<string> _notes = new();
  private List<string> _ccLines = new();

  public string GovernmentLine { get; private set; } = default!;
  public string AuthorityLine { get; private set; } = default!;
  public string? Motto { get; private set; }
  public IReadOnlyList<string> Notes { get => _notes; private set => _notes = value.ToList(); }
  public IReadOnlyList<string> CcLines { get => _ccLines; private set => _ccLines = value.ToList(); }
  public string? QrText { get; private set; }
  public string? QrBankName { get; private set; }
  public string? LogoLeftRef { get; private set; }
  public string? LogoRightRef { get; private set; }
  public bool Watermark { get; private set; }
  public string BoardReference { get; private set; } = default!;
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  private NoticeLetterhead() { }

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  public static NoticeLetterhead Create(NoticeLetterheadId id, LetterheadInput input, string? logoLeftRef, string? logoRightRef, DateOnly effectiveFrom)
  {
    var notes = Lines(input.Notes, 10, 500, "Note");
    var cc = Lines(input.CcLines, 10, 300, "Cc line");
    return new NoticeLetterhead
    {
      Id = id,
      GovernmentLine = Guard.RequiredText(input.GovernmentLine, 150, "Government line"),
      AuthorityLine = Guard.RequiredText(input.AuthorityLine, 150, "Authority line"),
      Motto = Guard.Text(input.Motto, 200, "Motto"),
      Notes = notes,
      CcLines = cc,
      QrText = Guard.Text(input.QrText, 200, "Text beside the QR"),
      QrBankName = Guard.Text(input.QrBankName, 60, "Bank name"),
      LogoLeftRef = logoLeftRef,
      LogoRightRef = logoRightRef,
      Watermark = input.Watermark,
      BoardReference = Guard.RequiredText(input.BoardReference, 100, "Reference"),
      EffectiveFrom = effectiveFrom
    };
  }

  /// A new version from a date; this row ends the day before (or, from the same day, is replaced).
  public NoticeLetterhead Revise(NoticeLetterheadId id, LetterheadInput input, string? logoLeftRef, string? logoRightRef, DateOnly effectiveFrom)
  {
    if (effectiveFrom < EffectiveFrom)
      throw new DomainException($"The letterhead in force started on {EffectiveFrom:yyyy-MM-dd}; a new one cannot start before it.");
    var next = Create(id, input, logoLeftRef, logoRightRef, effectiveFrom);
    EffectiveTo = effectiveFrom.AddDays(-1);
    return next;
  }

  private static List<string> Lines(IReadOnlyList<string>? lines, int max, int length, string what)
  {
    var list = (lines ?? []).Select(l => l?.Trim() ?? string.Empty).Where(l => l.Length > 0).ToList();
    if (list.Count > max)
      throw new DomainException($"At most {max} {what.ToLowerInvariant()}s.");
    foreach (var line in list)
      Guard.RequiredText(line, length, what);
    return list;
  }
}

public sealed record LetterheadInput(
  string GovernmentLine,
  string AuthorityLine,
  string? Motto,
  IReadOnlyList<string>? Notes,
  IReadOnlyList<string>? CcLines,
  string? QrText,
  string? QrBankName,
  bool Watermark,
  string BoardReference);

/// taxation.notice_table_layout: the table of particulars on printed notices, laid out by the admin: rows (label and
/// the heads each adds up) and columns (heading, printed by default). Every head a notice can carry has its own row (a
/// new head gets one), so there is no catch-all row; a head left without one (deactivated since) prints under its own
/// name, and the grand total always equals the amount due.
public class NoticeTableLayout : Aggregate<NoticeTableLayoutId>
{
  private readonly List<NoticeTableRow> _rows = new();
  private readonly List<NoticeTableColumn> _columns = new();

  public bool HideZeroRows { get; private set; }
  public string BoardReference { get; private set; } = default!;
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  public IReadOnlyList<NoticeTableRow> Rows => _rows.AsReadOnly();
  public IReadOnlyList<NoticeTableColumn> Columns => _columns.AsReadOnly();

  public IReadOnlyList<NoticeTableRow> OrderedRows => _rows.OrderBy(r => r.SortOrder).ToList();
  public IReadOnlyList<NoticeTableColumn> OrderedColumns => _columns.OrderBy(c => c.SortOrder).ToList();

  public IReadOnlySet<string> PlacedCodes => _rows.SelectMany(r => r.Sources.Select(s => s.SourceCode)).ToHashSet(StringComparer.Ordinal);

  private NoticeTableLayout() { }

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

  /// knownHeads: every head (code to name); requiredHeads: the codes of the active heads a notice can carry - each needs a row.
  public static NoticeTableLayout Create(NoticeTableLayoutId id, TableLayoutInput input, IReadOnlyDictionary<string, string> knownHeads, IReadOnlyCollection<string> requiredHeads,
      DateOnly effectiveFrom)
  {
    var layout = new NoticeTableLayout
    {
      Id = id,
      HideZeroRows = input.HideZeroRows,
      BoardReference = Guard.RequiredText(input.BoardReference, 100, "Reference"),
      EffectiveFrom = effectiveFrom
    };

    if (input.Rows.Count == 0)
      throw new DomainException("The table needs at least one row.");
    if (input.Rows.Count > 40)
      throw new DomainException("The table can have at most 40 rows.");

    var seen = new Dictionary<string, string>(StringComparer.Ordinal);
    var order = 0;
    foreach (var row in input.Rows)
    {
      var label = Guard.RequiredText(row.Label, 100, "Row label");
      var sources = row.HeadCodes.Select(c => c?.Trim().ToUpperInvariant() ?? string.Empty).Where(c => c.Length > 0).Distinct().ToList();
      if (sources.Count == 0)
        throw new DomainException($"\"{label}\": choose the heads it adds up.");
      foreach (var code in sources)
      {
        if (!knownHeads.ContainsKey(code))
          throw new DomainException($"\"{label}\": {code} is not a tax head.");
        if (seen.TryGetValue(code, out var other))
          throw new DomainException($"{knownHeads[code]} is in two rows: \"{other}\" and \"{label}\".");
        seen[code] = label;
      }
      layout._rows.Add(NoticeTableRow.Create(NoticeTableRowId.New(), id, label, ++order, sources));
    }

    var missing = requiredHeads.Where(c => !seen.ContainsKey(c)).Select(c => knownHeads.GetValueOrDefault(c, c)).ToList();
    if (missing.Count > 0)
      throw new DomainException($"Every head a notice can carry needs a row; {string.Join(", ", missing)} {(missing.Count == 1 ? "has" : "have")} none.");

    var columns = input.Columns.ToDictionary(c => c.Key);
    var colOrder = 0;
    foreach (var key in Enum.GetValues<TableColumnKey>())
    {
      var given = columns.GetValueOrDefault(key);
      var heading = Guard.RequiredText(given?.Heading ?? DefaultHeading(key), 100, "Column heading");
      var printed = key == TableColumnKey.Particular || (given?.PrintedByDefault ?? key != TableColumnKey.Basis);
      layout._columns.Add(NoticeTableColumn.Create(NoticeTableColumnId.New(), id, key, heading, printed, ++colOrder));
    }
    EnsureAmountColumn(layout._columns.Where(c => c.PrintedByDefault).Select(c => c.ColumnKey));
    return layout;
  }

  /// A new layout from a date; this one ends the day before.
  public void EndBefore(DateOnly nextStart)
  {
    if (nextStart < EffectiveFrom)
      throw new DomainException($"The layout in force started on {EffectiveFrom:yyyy-MM-dd}; a new one cannot start before it.");
    EffectiveTo = nextStart.AddDays(-1);
  }

  public IReadOnlyList<TableColumnKey> DefaultColumns => OrderedColumns.Where(c => c.PrintedByDefault).Select(c => c.ColumnKey).ToList();

  public static readonly TableColumnKey[] AmountColumns = [TableColumnKey.Arrears, TableColumnKey.Current, TableColumnKey.Received, TableColumnKey.Payable];

  public static void EnsureAmountColumn(IEnumerable<TableColumnKey> keys)
  {
    if (!keys.Any(k => AmountColumns.Contains(k)))
      throw new DomainException("The table needs at least one amount column.");
  }

  public static string DefaultHeading(TableColumnKey key) => key switch
  {
    TableColumnKey.Sno => "S.No",
    TableColumnKey.Particular => "Particular",
    TableColumnKey.Basis => "Basis of charge",
    TableColumnKey.Arrears => "Arrears",
    TableColumnKey.Current => "Current {fy}|Payable",
    TableColumnKey.Received => "Amount Received|{fy}",
    _ => "Amount Payable"
  };
}

public sealed record TableRowInput(string Label, IReadOnlyList<string> HeadCodes);
public sealed record TableColumnInput(TableColumnKey Key, string? Heading, bool PrintedByDefault);
public sealed record TableLayoutInput(IReadOnlyList<TableRowInput> Rows, IReadOnlyList<TableColumnInput> Columns, bool HideZeroRows, string BoardReference);

/// taxation.notice_table_row: a printed row and the heads it adds up (notice_table_row_source).
public class NoticeTableRow : Entity<NoticeTableRowId>
{
  private readonly List<NoticeTableRowSource> _sources = new();

  public NoticeTableLayoutId LayoutId { get; private set; } = default!;
  public string Label { get; private set; } = default!;
  public int SortOrder { get; private set; }
  public IReadOnlyList<NoticeTableRowSource> Sources => _sources.AsReadOnly();

  private NoticeTableRow() { }

  internal static NoticeTableRow Create(NoticeTableRowId id, NoticeTableLayoutId layoutId, string label, int sortOrder, IReadOnlyList<string> headCodes)
  {
    var row = new NoticeTableRow { Id = id, LayoutId = layoutId, Label = label, SortOrder = sortOrder };
    row._sources.AddRange(headCodes.Select(c => new NoticeTableRowSource(id, c)));
    return row;
  }
}

/// taxation.notice_table_row_source: one head code of a row (composite key row + code).
public class NoticeTableRowSource
{
  public NoticeTableRowId RowId { get; private set; } = default!;
  public string SourceCode { get; private set; } = default!;

  private NoticeTableRowSource() { }

  public NoticeTableRowSource(NoticeTableRowId rowId, string sourceCode)
  {
    RowId = rowId;
    SourceCode = sourceCode;
  }
}

/// taxation.notice_table_column: a column's heading ({fy} = the year, | = a new line) and whether it prints by default.
public class NoticeTableColumn : Entity<NoticeTableColumnId>
{
  public NoticeTableLayoutId LayoutId { get; private set; } = default!;
  public TableColumnKey ColumnKey { get; private set; }
  public string Heading { get; private set; } = default!;
  public bool PrintedByDefault { get; private set; }
  public int SortOrder { get; private set; }

  private NoticeTableColumn() { }

  internal static NoticeTableColumn Create(NoticeTableColumnId id, NoticeTableLayoutId layoutId, TableColumnKey key, string heading, bool printed, int sortOrder) => new()
  {
    Id = id,
    LayoutId = layoutId,
    ColumnKey = key,
    Heading = heading,
    PrintedByDefault = printed,
    SortOrder = sortOrder
  };
}
