using FluentValidation;

public sealed record ResolveAttributeSchemaQueryResult(IReadOnlyList<ResolvedAttributeDto> Attributes);

/// The effective data-entry form. Pass an AssetId to resolve for a concrete asset, or a
/// class (+ optional type / category) to preview the form a new asset would get.
public sealed record ResolveAttributeSchemaQuery(Guid? AssetId, Guid? AssetClassId, Guid? AssetTypeId, Guid? CategoryId)
  : IQuery<Result<ResolveAttributeSchemaQueryResult>>;

public class ResolveAttributeSchemaQueryValidator : AbstractValidator<ResolveAttributeSchemaQuery>
{
  public ResolveAttributeSchemaQueryValidator()
  {
    RuleFor(x => x)
      .Must(x => x.AssetId.HasValue || x.AssetClassId.HasValue || x.CategoryId.HasValue)
      .WithMessage("Provide assetId, or assetClassId / categoryId to resolve the attribute schema.");
  }
}
