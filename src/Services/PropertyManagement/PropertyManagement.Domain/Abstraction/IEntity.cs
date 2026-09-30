public interface IEntity<T> : IEntity
{
  public T Id { get; set; }
}

/// Audit columns every table carries (schema guide: created_at / created_by / updated_at / updated_by).
/// *_by hold the Identity service user id of the authenticated caller.
public interface IEntity
{
  public DateTime? CreatedAt { get; set; }
  public Guid? CreatedBy { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public Guid? UpdatedBy { get; set; }
}
