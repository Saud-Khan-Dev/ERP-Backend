/// Sale, Purchase, Exchange (Act s.6(4)(c)), Inheritance, Gift, Administrative Transfer ...
public sealed class TransferType : MasterData
{
  /// Gift / Inheritance: the transfer must record the relationship (Father -> Son ...).
  public bool RequiresRelationship { get; private set; }

  protected override void ApplyExtras(MasterExtras extras) =>
      RequiresRelationship = extras.RequiresRelationship ?? RequiresRelationship;
}
