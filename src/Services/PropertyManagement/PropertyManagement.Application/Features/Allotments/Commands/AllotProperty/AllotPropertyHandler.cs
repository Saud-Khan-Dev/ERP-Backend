using Microsoft.EntityFrameworkCore;

public class AllotPropertyHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, IAuditTrail audit)
  : ICommandHandler<AllotPropertyCommand, Result<AllotPropertyCommandResult>>
{
  public async Task<Result<AllotPropertyCommandResult>> Handle(AllotPropertyCommand command, CancellationToken cancellationToken)
  {
    var input = command.Allotment;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var allottee = await context.LoadOwnerAsync(command.AllotteeOwnerId, cancellationToken);
    var classification = await masters.GetAsync<PropertyClassification>(property.PropertyClassificationId.Value, cancellationToken);

    // two allotments of one plot at the same moment: the second save fails on the property's row version
    context.Touch(property);

    // GDA property only, not held privately, one allotment at a time per plot, not dated in the future
    var allotment = PropertyAllotment.Allot(
      AllotmentId.New(), property, classification, allottee,
      await codes.NextAsync(CodeSequenceKeys.Allotment, cancellationToken),
      await masters.GetAsync<AllotmentType>(input.AllotmentTypeId, cancellationToken),
      await masters.GetByCodeAsync<AllotmentStatus>(SystemMasterCodes.Active, cancellationToken),
      input.AllotmentDate, input.EffectiveDate, input.ExpiryDate, input.AllotmentLetterRef, input.Conditions, input.Remarks,
      await context.AllotmentInForceAsync(property.Id, except: null, cancellationToken),
      EntityLoaders.Today,
      await context.HeldSharesAsync(property.Id, cancellationToken));

    // the other kinds may coexist with an allotment (a shop rented on an allotted plot), so they only warn
    var warnings = AgreementOverlap.Check(
      new AgreementSpan(AgreementSpanKind.Allotment, allotment.AllotmentNo.Value, allottee.OwnerName.Value, allotment.TakesEffectOn, allotment.ExpiryDate, allotment.AllotmentTypeId),
      (await context.RunningOnAsync(property.Id, cancellationToken)).Where(s => s.Kind != AgreementSpanKind.Allotment));

    await context.Allotments.AddAsync(allotment, cancellationToken);
    audit.Record(PropertyAuditActions.Allotted, allotment, $"Allotted to {allottee.OwnerName.Value} ({allottee.OwnerCode.Value})");
    await context.SaveChangesAsync(cancellationToken);

    return Result<AllotPropertyCommandResult>.Success(new AllotPropertyCommandResult(allotment.Id.Value, allotment.AllotmentNo.Value, warnings));
  }
}

public static class HeldSharesLoader
{
  /// The shares held on a property now (ACTIVE + DISPUTED), with the holder's name and whether the holder is a
  /// government body (owner type GOVERNMENT): only land GDA holds can be allotted.
  public static async Task<List<PropertyAllotment.HeldShare>> HeldSharesAsync(this IApplicationDbContext context, PropertyId propertyId, CancellationToken cancellationToken)
  {
    var rows = await context.CurrentOwnershipsAsync(propertyId, cancellationToken);
    if (rows.Count == 0)
      return [];

    var ownerIds = rows.Select(r => r.OwnerId).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);
    var government = (await context.Set<OwnerType>().AsNoTracking().ToListAsync(cancellationToken))
      .Where(t => t.Is(SystemMasterCodes.OwnerTypeGovernment)).Select(t => t.Id).ToHashSet();

    return rows.GroupBy(r => r.OwnerId).Select(g =>
    {
      var owner = owners.GetValueOrDefault(g.Key);
      return new PropertyAllotment.HeldShare(owner?.OwnerName.Value ?? "an owner", g.Sum(r => r.OwnershipSharePct),
        owner is not null && government.Contains(owner.OwnerTypeId));
    }).ToList();
  }
}
