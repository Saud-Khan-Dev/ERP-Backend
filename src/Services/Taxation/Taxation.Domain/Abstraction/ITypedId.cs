/// A Guid wrapped in its own type (EmployeeId, PostId ...) so ids of different tables cannot be mixed up.
/// The DbContext maps every implementation to a uuid column by convention, so configurations never repeat the conversion.
public interface ITypedId
{
  Guid Value { get; }
}

public interface ITypedId<TSelf> : ITypedId where TSelf : ITypedId<TSelf>
{
  static abstract TSelf Of(Guid value);
}
