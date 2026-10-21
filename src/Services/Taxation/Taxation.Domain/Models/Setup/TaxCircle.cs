/// taxation.tax_circle: a tax administration area (C-I, C-II). Its towns are effective-dated, so a town can move between
/// circles without rewriting history; the office details are printed under the letterhead of its notices; the holder of
/// its Taxation Officer post (a tax_circle_staff row) signs them.
public class TaxCircle : Aggregate<TaxCircleId>
{
  private readonly List<TaxCircleTown> _towns = new();

  public string Code { get; private set; } = default!;
  public string Name { get; private set; } = default!;
  public Guid? OrgUnitId { get; private set; }
  public string? OfficeAddress { get; private set; }
  public string? OfficeFax { get; private set; }
  public string? OfficePhone { get; private set; }
  public int SortOrder { get; private set; }
  public bool IsActive { get; private set; }

  public IReadOnlyList<TaxCircleTown> Towns => _towns.AsReadOnly();

  private TaxCircle() { }

  public static TaxCircle Create(TaxCircleId id, string code, CircleDetails details)
  {
    var circle = new TaxCircle { Id = id, Code = Guard.Code(code, 20, "Circle code"), IsActive = true };
    circle.Update(details);
    return circle;
  }

  public void Update(CircleDetails details)
  {
    Name = Guard.RequiredText(details.Name, 100, "Circle name");
    OrgUnitId = details.OrgUnitId;
    OfficeAddress = Guard.Text(details.OfficeAddress, 300, "Office address");
    OfficeFax = Guard.Text(details.OfficeFax, 30, "Office fax");
    OfficePhone = Guard.Text(details.OfficePhone, 30, "Office phone");
    SortOrder = details.SortOrder;
  }

  public void SetActive(bool isActive, bool hasOpenTaxFiles)
  {
    if (!isActive && hasOpenTaxFiles)
      throw new DomainException($"Circle {Code} still has tax files. Move its towns to another circle first.");
    IsActive = isActive;
  }

  public TaxCircleTown? CurrentTown(Guid townId, DateOnly asOf) =>
      _towns.FirstOrDefault(t => t.TownId == townId && t.Covers(asOf));

  /// Brings a town into this circle from a date. The town's assignment in force on that date (in this circle or
  /// another) is closed the day before; an assignment that starts on or after the date blocks the move.
  public TaxCircleTown AssignTown(TaxCircleTownId id, Guid townId, string townCode, string? shortCode, DateOnly effectiveFrom, IReadOnlyList<TaxCircleTown> townHistory)
  {
    if (!IsActive)
      throw new DomainException($"Circle {Code} is inactive.");
    if (townId == Guid.Empty)
      throw new DomainException("Choose the town.");

    var later = townHistory.FirstOrDefault(t => t.EffectiveFrom >= effectiveFrom);
    if (later is not null)
      throw new DomainException($"The town already has a circle assignment from {later.EffectiveFrom:yyyy-MM-dd}. Assign it from a later date.");

    var current = townHistory.FirstOrDefault(t => t.Covers(effectiveFrom));
    if (current is not null && current.CircleId == Id)
      throw new DomainException($"The town is already in circle {Code}. Change its short code instead.");
    current?.Close(effectiveFrom.AddDays(-1));

    var town = TaxCircleTown.Create(id, Id, townId, townCode, shortCode, effectiveFrom);
    _towns.Add(town);
    return town;
  }

  /// The town leaves the circle after a last day (and is in no circle from the next day until assigned again).
  public void EndTown(TaxCircleTownId id, DateOnly lastDay, IReadOnlyList<TaxCircleTown> townHistory)
  {
    var town = _towns.FirstOrDefault(t => t.Id == id) ?? throw new DomainException("The town assignment was not found in this circle.");
    if (townHistory.Any(t => t.Id != town.Id && t.EffectiveFrom > town.EffectiveFrom))
      throw new DomainException("A later assignment of the town follows; it already ends this one.");
    if (town.EffectiveTo is not null)
      throw new DomainException($"The town left the circle after {town.EffectiveTo:dd MMM yyyy} already.");
    town.Close(lastDay);
  }

  /// An assignment made from a later date by mistake is taken back before it is in force; the town's assignment it would
  /// have ended runs on.
  public TaxCircleTown? VoidTown(TaxCircleTownId id, DateOnly today, IReadOnlyList<TaxCircleTown> townHistory)
  {
    var town = _towns.FirstOrDefault(t => t.Id == id) ?? throw new DomainException("The town assignment was not found in this circle.");
    if (town.EffectiveFrom <= today)
      throw new DomainException($"The assignment is in force since {town.EffectiveFrom:dd MMM yyyy}; assign the town to another circle from a new date instead.");
    if (townHistory.Any(t => t.Id != town.Id && t.EffectiveFrom > town.EffectiveFrom))
      throw new DomainException("A later assignment of the town follows; take that one back first.");
    var previous = townHistory.Where(t => t.Id != town.Id && t.EffectiveFrom < town.EffectiveFrom).OrderByDescending(t => t.EffectiveFrom).FirstOrDefault();
    previous?.Reopen(town.EffectiveFrom.AddDays(-1));
    _towns.Remove(town);
    return previous;
  }
}

public sealed record CircleDetails(string Name, Guid? OrgUnitId, string? OfficeAddress, string? OfficeFax, string? OfficePhone, int SortOrder);

/// taxation.tax_circle_town: which circle a Property town belongs to, from a date. The short code (NG for Nathiagali)
/// is printed as "Town" on notices.
public class TaxCircleTown : Entity<TaxCircleTownId>
{
  public TaxCircleId CircleId { get; private set; } = default!;
  public Guid TownId { get; private set; }
  public string TownCode { get; private set; } = default!;
  public string? ShortCode { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  private TaxCircleTown() { }

  internal static TaxCircleTown Create(TaxCircleTownId id, TaxCircleId circleId, Guid townId, string townCode, string? shortCode, DateOnly effectiveFrom) => new()
  {
    Id = id,
    CircleId = circleId,
    TownId = townId,
    TownCode = Guard.RequiredText(townCode, 20, "Town code"),
    ShortCode = Guard.Text(shortCode, 5, "Short code")?.ToUpperInvariant(),
    EffectiveFrom = effectiveFrom
  };

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo.Value >= date);

  public void SetShortCode(string? shortCode) => ShortCode = Guard.Text(shortCode, 5, "Short code")?.ToUpperInvariant();

  internal void Close(DateOnly lastDay)
  {
    if (lastDay < EffectiveFrom)
      throw new DomainException($"The town's assignment from {EffectiveFrom:dd MMM yyyy} cannot end before it starts.");
    EffectiveTo = lastDay;
  }

  /// The assignment that ended because of one taken back runs on again.
  internal void Reopen(DateOnly endedOn)
  {
    if (EffectiveTo == endedOn)
      EffectiveTo = null;
  }
}

/// taxation.tax_circle_staff: a sanctioned HRM post that works for a circle (or the whole Section when no circle). The
/// person is whoever holds the post on the date, so staffing follows HRM postings.
public class TaxCircleStaff : Aggregate<TaxCircleStaffId>
{
  public TaxCircleId? CircleId { get; private set; }
  public Guid PostId { get; private set; }
  public TaxRole TaxRole { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  private TaxCircleStaff() { }

  public static TaxCircleStaff Create(TaxCircleStaffId id, TaxCircleId? circleId, Guid postId, TaxRole role, DateOnly effectiveFrom)
  {
    if (postId == Guid.Empty)
      throw new DomainException("Choose the HRM post.");
    if (role == TaxRole.DdTax && circleId is not null)
      throw new DomainException("The Deputy Director (Taxation) works for the whole Section, not one circle.");
    if (role != TaxRole.DdTax && circleId is null)
      throw new DomainException("Choose the circle the post works for.");
    return new TaxCircleStaff { Id = id, CircleId = circleId, PostId = postId, TaxRole = role, EffectiveFrom = effectiveFrom };
  }

  public bool Covers(DateOnly date) => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo.Value >= date);

  public void End(DateOnly lastDay)
  {
    if (EffectiveTo is not null)
      throw new DomainException("The post already stopped working for the circle.");
    if (lastDay < EffectiveFrom)
      throw new DomainException("The end date cannot be before the start date.");
    EffectiveTo = lastDay;
  }
}

/// taxation.category_map: which tax category (RES / COM / EXEMPT) a Property property type is assessed in.
public class CategoryMap : Aggregate<CategoryMapId>
{
  public Guid PropertyTypeId { get; private set; }
  public string PropertyTypeCode { get; private set; } = default!;
  public TaxCategory TaxCategory { get; private set; }

  private CategoryMap() { }

  public static CategoryMap Create(CategoryMapId id, Guid propertyTypeId, string propertyTypeCode, TaxCategory category)
  {
    if (propertyTypeId == Guid.Empty)
      throw new DomainException("Choose the property type.");
    return new CategoryMap { Id = id, PropertyTypeId = propertyTypeId, PropertyTypeCode = Guard.RequiredText(propertyTypeCode, 30, "Property type code"), TaxCategory = category };
  }

  public void Change(TaxCategory category) => TaxCategory = category;

  /// The category when a property type has no mapping: Property's seeded RESIDENTIAL and COMMERCIAL types map by
  /// their code; any other type must be mapped first (Mixed Use and Institutional are open points).
  public static TaxCategory? Default(string? propertyTypeCode) => propertyTypeCode?.Trim().ToUpperInvariant() switch
  {
    "RESIDENTIAL" => TaxCategory.Res,
    "COMMERCIAL" => TaxCategory.Com,
    _ => null
  };
}
