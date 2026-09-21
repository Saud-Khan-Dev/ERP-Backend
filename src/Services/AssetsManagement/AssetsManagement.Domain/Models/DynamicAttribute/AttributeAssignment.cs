using System.Text.Json;

/// *** THE DYNAMIC CORE ***
/// Binds an AttributeDefinition to exactly ONE scope (class / type / category / asset).
/// An asset resolves CLASS -> TYPE -> CATEGORY(root..leaf) -> ASSET, later levels overriding earlier ones
/// for the same AttributeDefinitionId.
public class AttributeAssignment : Aggregate<AttributeAssignmentId>
{
  public AttributeDefinitionId AttributeDefinitionId { get; private set; } = default!;
  public AttributeScope Scope { get; private set; }

  // exactly one of these four is populated, matching Scope (CHECK constraint in the DB, guarded here too)
  public AssetClassId? AssetClassId { get; private set; }
  public AssetTypeId? AssetTypeId { get; private set; }
  public AssetCategoryId? CategoryId { get; private set; }
  public AssetId? AssetId { get; private set; }

  public AttributeGroupId? AttributeGroupId { get; private set; }

  // ---- per-assignment overrides of the definition ----
  public string? LabelOverride { get; private set; }
  public bool IsRequired { get; private set; }
  public bool IsReadonly { get; private set; }
  public bool IsSearchable { get; private set; }
  public bool IsFilterable { get; private set; }
  public bool IsVisibleInList { get; private set; }
  public bool InheritToChildren { get; private set; }
  public JsonElement? DefaultValue { get; private set; }
  public int? DisplayOrder { get; private set; }

  // ---- conditional visibility ----
  public AttributeAssignmentId? DependsOnAssignmentId { get; private set; }
  public JsonElement? DependsOnValue { get; private set; }

  public bool IsActive { get; private set; }

  /// Whether the value needs a typed row in asset_attribute_value.
  public bool IsProjected => IsFilterable || IsSearchable;

  public static AttributeAssignment Create(
      AttributeAssignmentId id,
      AttributeDefinitionId attributeDefinitionId,
      AttributeScopeTarget target,
      AttributeGroupId? attributeGroupId,
      AttributeAssignmentOptions options)
  {
    ArgumentNullException.ThrowIfNull(attributeDefinitionId);
    ArgumentNullException.ThrowIfNull(target);

    var assignment = new AttributeAssignment
    {
      Id = id,
      AttributeDefinitionId = attributeDefinitionId,
      Scope = target.Scope,
      AssetClassId = target.AssetClassId,
      AssetTypeId = target.AssetTypeId,
      CategoryId = target.CategoryId,
      AssetId = target.AssetId,
      IsActive = true
    };

    assignment.Apply(attributeGroupId, options);
    return assignment;
  }

  public void Update(AttributeGroupId? attributeGroupId, AttributeAssignmentOptions options, bool isActive)
  {
    Apply(attributeGroupId, options);
    IsActive = isActive;
  }

  private void Apply(AttributeGroupId? attributeGroupId, AttributeAssignmentOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    if (options.DependsOnAssignmentId is not null && options.DependsOnAssignmentId == Id)
      throw new DomainException("An attribute cannot depend on itself.");

    if (options.DependsOnAssignmentId is null && options.DependsOnValue is not null)
      throw new DomainException("depends_on_value requires depends_on_assignment_id.");

    if (options.LabelOverride is { Length: > 150 })
      throw new DomainException("label_override cannot exceed 150 characters.");

    AttributeGroupId = attributeGroupId;
    LabelOverride = string.IsNullOrWhiteSpace(options.LabelOverride) ? null : options.LabelOverride.Trim();
    IsRequired = options.IsRequired;
    IsReadonly = options.IsReadonly;
    IsSearchable = options.IsSearchable;
    IsFilterable = options.IsFilterable;
    IsVisibleInList = options.IsVisibleInList;
    InheritToChildren = Scope == AttributeScope.Category && options.InheritToChildren;
    DefaultValue = options.DefaultValue;
    DisplayOrder = options.DisplayOrder;
    DependsOnAssignmentId = options.DependsOnAssignmentId;
    DependsOnValue = options.DependsOnValue;
  }
}

/// Exactly one target id, matching the scope. Built through the factory methods so the invariant always holds.
public sealed record AttributeScopeTarget
{
  public AttributeScope Scope { get; }
  public AssetClassId? AssetClassId { get; }
  public AssetTypeId? AssetTypeId { get; }
  public AssetCategoryId? CategoryId { get; }
  public AssetId? AssetId { get; }

  private AttributeScopeTarget(AttributeScope scope, AssetClassId? classId, AssetTypeId? typeId, AssetCategoryId? categoryId, AssetId? assetId)
  {
    Scope = scope;
    AssetClassId = classId;
    AssetTypeId = typeId;
    CategoryId = categoryId;
    AssetId = assetId;
  }

  public static AttributeScopeTarget ForClass(AssetClassId id) => new(AttributeScope.AssetClass, id, null, null, null);
  public static AttributeScopeTarget ForType(AssetTypeId id) => new(AttributeScope.AssetType, null, id, null, null);
  public static AttributeScopeTarget ForCategory(AssetCategoryId id) => new(AttributeScope.Category, null, null, id, null);
  public static AttributeScopeTarget ForAsset(AssetId id) => new(AttributeScope.Asset, null, null, null, id);

  public static AttributeScopeTarget Of(AttributeScope scope, Guid? classId, Guid? typeId, Guid? categoryId, Guid? assetId)
  {
    var populated = new[] { classId, typeId, categoryId, assetId }.Count(v => v.HasValue && v.Value != Guid.Empty);
    if (populated != 1)
      throw new DomainException("Exactly one of asset_class_id, asset_type_id, category_id or asset_id must be provided.");

    return scope switch
    {
      AttributeScope.AssetClass when classId.HasValue => ForClass(AssetClassId.Of(classId.Value)),
      AttributeScope.AssetType when typeId.HasValue => ForType(AssetTypeId.Of(typeId.Value)),
      AttributeScope.Category when categoryId.HasValue => ForCategory(AssetCategoryId.Of(categoryId.Value)),
      AttributeScope.Asset when assetId.HasValue => ForAsset(AssetId.Of(assetId.Value)),
      _ => throw new DomainException($"The provided target id does not match scope {scope}.")
    };
  }
}

public sealed record AttributeAssignmentOptions(
    string? LabelOverride = null,
    bool IsRequired = false,
    bool IsReadonly = false,
    bool IsSearchable = false,
    bool IsFilterable = false,
    bool IsVisibleInList = false,
    bool InheritToChildren = true,
    JsonElement? DefaultValue = null,
    int? DisplayOrder = null,
    AttributeAssignmentId? DependsOnAssignmentId = null,
    JsonElement? DependsOnValue = null);
