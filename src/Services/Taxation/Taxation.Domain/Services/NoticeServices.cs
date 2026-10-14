using System.Globalization;
using System.Text;

/// The numbers the module issues. Running numbers come from database sequences; these shape them.
public static class Numbers
{
  private const int CodeSpace = 1_679_616; // 36^4

  /// The printed notice reference, as on the Section's notices: GDA-TAX-2026-4VX4. The four characters are a bijective
  /// mix of a running number (n x 1013 + 7919 mod 36^4; 1013 is prime to 36), so references do not reveal how many
  /// notices were issued and never repeat within 1.68 million.
  public static string NoticeNo(int year, long sequence)
  {
    var mixed = (sequence % CodeSpace * 1013 + 7919) % CodeSpace;
    return $"GDA-TAX-{year}-{Base36(mixed).PadLeft(4, '0')}";
  }

  /// The endorsement number, running per circle and year: TAX/C-I/2026-27/00001.
  public static string EndstNo(string circleCode, int fiscalYear, int sequence) => $"TAX/{circleCode}/{FiscalYear.Label(fiscalYear)}/{sequence:00000}";

  public static string DemandNo(int fiscalYear, long sequence) => $"DMD-{FiscalYear.Label(fiscalYear)}-{sequence:00000}";

  /// The Section's file code until Property issues one: GDA-NATHIAGALI-COM-001.
  public static string FileNo(string townCode, TaxCategory category, int sequence) =>
      $"GDA-{townCode.Trim().ToUpperInvariant()}-{(category == TaxCategory.Com ? "COM" : "RSD")}-{sequence:000}";

  public static string FilePrefix(string townCode, TaxCategory category) => $"GDA-{townCode.Trim().ToUpperInvariant()}-{(category == TaxCategory.Com ? "COM" : "RSD")}-";

  /// The constant 1Link / 1Bill consumer reference: the prefix and a running number, 12 digits in all.
  public static string Psid(string prefix, long sequence)
  {
    var digits = Math.Max(4, 12 - prefix.Length);
    return prefix + sequence.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
  }

  public static string FeeNo(long sequence) => $"FEE-{sequence:00000}";

  public static string ClearanceNo(long sequence) => $"CLR-{sequence:00000}";

  public static string ReceiptNo(long sequence) => $"MR-{sequence:00000}";

  /// 6011 0000 0022
  public static string FormatPsid(string? psid) => string.IsNullOrEmpty(psid) ? "" : string.Join(' ', Enumerable.Range(0, (psid.Length + 3) / 4).Select(i => psid.Substring(i * 4, Math.Min(4, psid.Length - i * 4))));

  private static string Base36(long value)
  {
    const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    if (value == 0) return "0";
    var text = new StringBuilder();
    while (value > 0)
    {
      text.Insert(0, digits[(int)(value % 36)]);
      value /= 36;
    }
    return text.ToString();
  }
}

/// A dynamic merchant-presented payment QR in the EMVCo format that banking apps and Raast read: GDA as merchant, the
/// amount in PKR, and the property's constant 1Link reference as the bill number, so a QR payment posts against the
/// same reference.
public static class PaymentQr
{
  public static string Payload(TaxSettings s, string psid, decimal amount, string label)
  {
    var body = Tlv("00", "01") + Tlv("01", "12")
      + Tlv("26", Tlv("00", Ascii(s.QrGuid)) + Tlv("01", Ascii(s.QrMerchantId)))
      + Tlv("52", s.QrMcc) + Tlv("53", "586")
      + Tlv("54", Math.Max(0, Rupees.Round(amount)).ToString("0.00", CultureInfo.InvariantCulture))
      + Tlv("58", "PK")
      + Tlv("59", Cut(Ascii(s.QrMerchantName), 25)) + Tlv("60", Cut(Ascii(s.QrCity), 15))
      + Tlv("62", Tlv("01", psid) + Tlv("05", Cut(Ascii(label), 25)) + Tlv("08", "GDA TAX"));
    var withCrc = body + "6304";
    return withCrc + Crc16(withCrc);
  }

  /// CRC-16/CCITT-FALSE (poly 0x1021, start 0xFFFF), as EMVCo requires, in four upper-case hex digits.
  public static string Crc16(string text)
  {
    var crc = 0xFFFF;
    foreach (var c in text)
    {
      crc ^= c << 8;
      for (var k = 0; k < 8; k++)
        crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
    }
    return crc.ToString("X4", CultureInfo.InvariantCulture);
  }

  private static string Tlv(string id, string value)
  {
    if (value.Length > 99) value = value[..99];
    return id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;
  }

  private static string Ascii(string? text) => new string((text ?? "").Where(c => c >= 0x20 && c <= 0x7E).ToArray()).ToUpperInvariant();

  private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}

/// Working days for notice periods: weekends and the HRM holiday calendar are skipped (calendar days when the setting
/// says so).
public sealed class WorkCalendar(bool countWorkingDays, IReadOnlySet<DateOnly> holidays)
{
  public bool IsWorkingDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(date);

  public DateOnly AddDays(DateOnly from, int days)
  {
    if (!countWorkingDays)
      return from.AddDays(days);
    var date = from;
    for (var counted = 0; counted < days;)
    {
      date = date.AddDays(1);
      if (IsWorkingDay(date))
        counted++;
    }
    return date;
  }
}

/// The table of particulars of a demand, laid out by the admin's layout: arrears (earlier years' dues billed on it),
/// current (this year's heads and surcharges), received (what payments settled) and payable, per row. A head in no row
/// goes to the "other" row, so the payable total always equals what is owed.
public static class NoticeTable
{
  public static NoticeParticulars Build(Demand demand, IReadOnlyList<DemandItem> items, IReadOnlyDictionary<TaxHeadId, string> headCodes, NoticeTableLayout layout)
  {
    string Code(TaxHeadId id) => headCodes.TryGetValue(id, out var code) ? code : HeadCodes.Misc;
    var basisByHead = demand.Lines.Where(l => l.Amount > 0 && l.Explanation is not null).GroupBy(l => Code(l.HeadId)).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(l => l.Explanation)));

    ParticularRow Row(string label, IReadOnlyList<string> codes)
    {
      var mine = items.Where(i => codes.Contains(Code(i.HeadId))).ToList();
      var arrears = mine.Where(i => i.IsArrears).Sum(i => i.Amount);
      var current = mine.Where(i => !i.IsArrears).Sum(i => i.Amount);
      var received = mine.Sum(i => Math.Min(i.Paid, i.Amount));
      var basis = string.Join("; ", codes.Where(basisByHead.ContainsKey).Select(c => basisByHead[c]));
      return new ParticularRow(label, codes, basis.Length == 0 ? null : basis, arrears, current, received, mine.Sum(i => i.Outstanding));
    }

    var rows = layout.OrderedRows.Select(r => Row(r.Label, r.Sources.Select(s => s.SourceCode).ToList())).ToList();
    var placed = layout.OrderedRows.SelectMany(r => r.Sources.Select(s => s.SourceCode)).ToHashSet();
    var otherCodes = items.Select(i => Code(i.HeadId)).Where(c => !placed.Contains(c)).Distinct().ToList();
    if (otherCodes.Count > 0)
    {
      var other = Row(layout.OtherLabel, otherCodes);
      if (other.Arrears != 0 || other.Current != 0 || other.Received != 0)
        rows.Add(other);
    }
    if (layout.HideZeroRows)
      rows = rows.Where(r => r.Arrears != 0 || r.Current != 0 || r.Received != 0 || r.Payable != 0).ToList();

    var totals = new ParticularTotals(rows.Sum(r => r.Arrears), rows.Sum(r => r.Current), rows.Sum(r => r.Received), rows.Sum(r => r.Payable));
    return new NoticeParticulars(demand.FiscalYear, rows, totals);
  }

  /// A paragraph with its placeholders filled as plain text ({amount} -> "Rs. 78,387/=").
  public static string FillParagraph(string paragraph, IReadOnlyDictionary<string, string> values) =>
      System.Text.RegularExpressions.Regex.Replace(paragraph, @"\{(\w+)\}", m =>
      {
        var key = m.Groups[1].Value.ToLowerInvariant().Replace("replyby", "reply_by");
        return values.TryGetValue(key, out var value) ? value : m.Value;
      });

  public static string Amount(decimal value) => "Rs. " + Rupees.Round(value).ToString("#,0", CultureInfo.InvariantCulture) + "/=";
}
