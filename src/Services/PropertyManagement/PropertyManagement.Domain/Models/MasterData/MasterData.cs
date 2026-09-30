/// Shape shared by every master (lookup) table: town, property_type, lease_status ...
/// Admins maintain these from a settings screen, so GDA can add a type or status without a code
/// change. Each master is its own table (so foreign keys stay real); this base class only shares
/// the columns and rules.
public abstract class MasterData : Entity<MasterId>
{
  public const int NameMaxLength = 150;

  /// Immutable once created: it is what code and reports compare against.
  public MasterCode Code { get; private set; } = default!;
  public Name Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public int SortOrder { get; private set; }
  public bool IsActive { get; private set; }

  public static T Create<T>(MasterId id, MasterCode code, Name name, string? description, int sortOrder, MasterExtras? extras = null)
      where T : MasterData, new()
  {
    ArgumentNullException.ThrowIfNull(code);

    var master = new T { Id = id, Code = code, IsActive = true };
    master.Apply(name, description, sortOrder, extras ?? MasterExtras.None);
    return master;
  }

  public void Update(Name name, string? description, int sortOrder, MasterExtras? extras = null) =>
      Apply(name, description, sortOrder, extras ?? MasterExtras.None);

  /// Masters are never deleted, only deactivated (schema guide, rule 9): old records keep pointing at them.
  public void Activate() => IsActive = true;
  public void Deactivate() => IsActive = false;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"'{Name.Value}' ({Code.Value}) is inactive and cannot be used.");
  }

  /// Extra columns a few masters carry (measurement_unit.factor_to_base, document_type.storage_folder ...).
  protected virtual void ApplyExtras(MasterExtras extras) { }

  private void Apply(Name name, string? description, int sortOrder, MasterExtras extras)
  {
    ArgumentNullException.ThrowIfNull(name);

    if (name.Value.Length > NameMaxLength)
      throw new DomainException($"Name cannot exceed {NameMaxLength} characters.");

    Name = name;
    Description = Guard.Text(description, 2000, "Description");
    SortOrder = sortOrder;
    ApplyExtras(extras);
  }
}

/// The optional extra columns, all in one bag so the generic master API can carry them.
public sealed record MasterExtras(
    decimal? FactorToBase = null,
    bool? IsBase = null,
    string? StorageFolder = null,
    bool? RequiresRelationship = null)
{
  public static MasterExtras None { get; } = new();
}
