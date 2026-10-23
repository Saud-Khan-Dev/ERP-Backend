/// An employment type HR names in Configuration (Regular, Contract, ...). Each follows the rules of one basis; the basis
/// is fixed once anyone uses the type. At most one active type follows the Regular rules and one the Deputation rules,
/// so a Regularization or a Deputation (in) knows which type the person takes.
public class EmploymentType : Aggregate<EmploymentTypeId>
{
  public const int NameMaxLength = 100;
  public const int DescriptionMaxLength = 200;

  public string Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public EmploymentBasis Basis { get; private set; }
  public bool IsActive { get; private set; }

  /// Only one active type may follow these rules.
  public static bool IsSingle(EmploymentBasis basis) => basis is EmploymentBasis.Regular or EmploymentBasis.Deputation;

  public static EmploymentType Create(EmploymentTypeId id, string name, string? description, EmploymentBasis basis)
  {
    var type = new EmploymentType { Id = id, IsActive = true, Basis = basis };
    type.Update(name, description, basis, inUse: false);
    return type;
  }

  public void Update(string name, string? description, EmploymentBasis basis, bool inUse)
  {
    if (inUse && basis != Basis)
      throw new DomainException($"'{Name}' is in use, so the rules it follows cannot change. Add a new employment type instead.");

    Name = Guard.RequiredText(name, NameMaxLength, "Employment type");
    Description = Guard.Text(description, DescriptionMaxLength, "Description");
    Basis = basis;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Employment type '{Name}' is inactive.");
  }
}
