public class GetOwnerHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetOwnerQuery, Result<GetOwnerQueryResult>>
{
  public async Task<Result<GetOwnerQueryResult>> Handle(GetOwnerQuery query, CancellationToken cancellationToken)
  {
    var owner = await context.LoadOwnerAsync(query.Id, cancellationToken);

    var refs = await masters.Refs()
        .Add<OwnerType>(owner.OwnerTypeId)
        .Add<ContactType>(owner.Contacts.Select(c => c.ContactTypeId))
        .LoadAsync(cancellationToken);

    return Result<GetOwnerQueryResult>.Success(new GetOwnerQueryResult(owner.ToDto(refs)));
  }
}
