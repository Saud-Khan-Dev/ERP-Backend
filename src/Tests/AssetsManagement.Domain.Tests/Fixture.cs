/// Small builders for the Assets domain tests.
public static class Fixture
{
  public static readonly ActorRef Clerk = new(Guid.NewGuid(), "Clerk One");

  public static AssetClass Physical() => AssetClass.Create(AssetClassId.Of(Guid.NewGuid()), LookupCode.Of("PHYSICAL"), Name.Of("Physical"), null, 1);

  public static AssetType Movable(AssetClass cls, bool depreciable = true) =>
    AssetType.Create(AssetTypeId.Of(Guid.NewGuid()), cls.Id, LookupCode.Of("MOVABLE"), Name.Of("Movable"), null, depreciable, false, false, 1);

  public static AssetCategory Laptops(AssetClass cls, AssetType type) =>
    AssetCategory.Create(AssetCategoryId.Of(Guid.NewGuid()), cls.Id, type.Id, null, LookupCode.Of("LAPTOP"), Name.Of("Laptops"), null, 1);

  public static AssetStatus Status(string code, bool terminal = false, bool allowsAssignment = true) =>
    AssetStatus.Create(AssetStatusId.Of(Guid.NewGuid()), LookupCode.Of(code), Name.Of(code), null, terminal, !terminal && allowsAssignment, null, 1);

  public static DepreciationMethod Method(DepreciationMethodKind kind) =>
    DepreciationMethod.Create(DepreciationMethodId.Of(Guid.NewGuid()), LookupCode.Of(kind.ToString().ToUpperInvariant()), Name.Of(kind.ToString()), null, kind, isSystem: true);

  public static (Asset Asset, AssetType Type, AssetStatus Status) Asset()
  {
    var cls = Physical();
    var type = Movable(cls);
    var status = Status("IN_SERVICE");
    var asset = global::Asset.Create(AssetId.Of(Guid.NewGuid()), AssetCode.Of("AST-000100"), Name.Of("Dell laptop", 200), null, OwnershipType.Owned,
      cls, type, Laptops(cls, type), status, CustodyRef.None, null, null);
    return (asset, type, status);
  }

  public static AssetDepreciationSchedule Schedule(decimal opening, int months, DateOnly start, decimal salvage = 0m,
      DepreciationMethodKind kind = DepreciationMethodKind.StraightLine, decimal? rate = null, DepreciationMethod? method = null)
  {
    var (asset, type, _) = Asset();
    return AssetDepreciationSchedule.Create(AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, type, method ?? Method(kind), opening, months, salvage, rate, start);
  }
}
