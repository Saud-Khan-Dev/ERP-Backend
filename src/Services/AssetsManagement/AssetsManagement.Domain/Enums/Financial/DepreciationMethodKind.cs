/// The calculation a depreciation method uses - what the engine keys on (never the method's code or name).
public enum DepreciationMethodKind
{
  /// The same amount every month.
  StraightLine,
  /// A fixed yearly share of what is left; switches to straight line once that gives more.
  DecliningBalance,
  /// By use: entries are added by hand.
  UnitsOfProduction
}
