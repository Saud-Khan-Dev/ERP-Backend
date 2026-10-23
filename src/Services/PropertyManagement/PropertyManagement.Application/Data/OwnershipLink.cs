/// Which owner holds or held a share in which property: a read-only view of property_ownership that a town scope never
/// narrows, for the one question the scope cannot answer on its own - "does this owner hold land anywhere at all?"
/// (an owner who holds none stays visible to everyone, so new owners can be registered and used).
public sealed class OwnershipLink
{
  public OwnerId OwnerId { get; private set; } = default!;
  public PropertyId PropertyId { get; private set; } = default!;
}
