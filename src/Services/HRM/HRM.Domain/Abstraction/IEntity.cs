public interface IEntity<T> : IEntity
{
  public T Id { get; set; }
}

/// Audit columns (created_at / created_by / updated_at / updated_by). The schema gives each table only some of them;
/// the EF configuration maps the ones a table has. *_by hold the Identity user id of the authenticated caller.
public interface IEntity
{
  public DateTime? CreatedAt { get; set; }
  public Guid? CreatedBy { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public Guid? UpdatedBy { get; set; }
}
