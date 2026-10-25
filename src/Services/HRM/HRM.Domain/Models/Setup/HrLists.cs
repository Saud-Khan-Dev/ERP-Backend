// Lists HR keeps in Configuration (People & service, Pay) instead of in code. Values are deactivated, never deleted
// once used, so old records keep their names.

/// Primary, Matric, Bachelors ... Rank orders them: the higher rank is the higher qualification.
public class QualificationLevel : Aggregate<QualificationLevelId>
{
  public string Name { get; private set; } = default!;
  public int Rank { get; private set; }
  public bool IsActive { get; private set; }

  public static QualificationLevel Create(QualificationLevelId id, string name, int rank)
  {
    var level = new QualificationLevel { Id = id, IsActive = true };
    level.Update(name, rank);
    return level;
  }

  public void Update(string name, int rank)
  {
    Name = Guard.RequiredText(name, 100, "Qualification level");
    if (rank is < 1 or > 99)
      throw new DomainException("The rank is 1 to 99 (higher = higher qualification).");
    Rank = rank;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Qualification level '{Name}' is inactive.");
  }
}

/// Spouse, Son, Daughter ... - how a family member is related to the employee.
public class FamilyRelation : Aggregate<FamilyRelationId>
{
  public string Name { get; private set; } = default!;
  public bool IsActive { get; private set; }

  public static FamilyRelation Create(FamilyRelationId id, string name) =>
      new() { Id = id, Name = Guard.RequiredText(name, 60, "Relation"), IsActive = true };

  public void Update(string name) => Name = Guard.RequiredText(name, 60, "Relation");

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Relation '{Name}' is inactive.");
  }
}

/// How a regular employee is employed (scheduled seat, GDA personal ...). Only regular employees have one.
public class EmploymentMethod : Aggregate<EmploymentMethodId>
{
  public string Name { get; private set; } = default!;
  public string? Description { get; private set; }
  public bool IsActive { get; private set; }

  public static EmploymentMethod Create(EmploymentMethodId id, string name, string? description)
  {
    var method = new EmploymentMethod { Id = id, IsActive = true };
    method.Update(name, description);
    return method;
  }

  public void Update(string name, string? description)
  {
    Name = Guard.RequiredText(name, 100, "Employment method");
    Description = Guard.Text(description, 200, "Description");
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Employment method '{Name}' is inactive.");
  }
}

/// Whether an adjustment raises net pay (entered positive), lowers it (entered negative) or may do either.
public enum AdjustmentDirection { Raises, Lowers, Either }

/// A kind of hand-entered pay slip adjustment (arrears, recovery, bonus ...). Arrears and Recovery are the system's
/// own: the arrears run writes them, so they are never renamed into something else or switched off.
public class AdjustmentType : Aggregate<AdjustmentTypeId>
{
  public static readonly AdjustmentTypeId ArrearsId = AdjustmentTypeId.Of(Guid.Parse("7a2e1c40-0001-4b6f-8d21-000000000001"));
  public static readonly AdjustmentTypeId RecoveryId = AdjustmentTypeId.Of(Guid.Parse("7a2e1c40-0002-4b6f-8d21-000000000002"));

  public string Name { get; private set; } = default!;
  public AdjustmentDirection Direction { get; private set; }
  public bool IsActive { get; private set; }

  public bool IsSystem => Id == ArrearsId || Id == RecoveryId;

  public static AdjustmentType Create(AdjustmentTypeId id, string name, AdjustmentDirection direction)
  {
    var type = new AdjustmentType { Id = id, IsActive = true };
    type.Update(name, direction);
    return type;
  }

  public void Update(string name, AdjustmentDirection direction)
  {
    if (IsSystem && Direction != direction && Name is not null)
      throw new DomainException($"'{Name}' is written by the arrears run; whether it raises or lowers pay cannot change.");
    Name = Guard.RequiredText(name, 100, "Adjustment type");
    Direction = direction;
  }

  public void SetActive(bool isActive)
  {
    if (!isActive && IsSystem)
      throw new DomainException($"'{Name}' is written by the arrears run; it cannot be switched off.");
    IsActive = isActive;
  }

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Adjustment type '{Name}' is inactive.");
  }

  /// Refuses an amount whose sign does not fit the type.
  public void EnsureFits(decimal amount)
  {
    if (Direction == AdjustmentDirection.Lowers && amount > 0)
      throw new DomainException($"{Name} lowers net pay: enter it as a negative amount.");
    if (Direction == AdjustmentDirection.Raises && amount < 0)
      throw new DomainException($"{Name} raises net pay: enter it as a positive amount.");
  }
}
