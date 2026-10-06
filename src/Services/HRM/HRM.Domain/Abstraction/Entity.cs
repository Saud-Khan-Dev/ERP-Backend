public abstract class Entity<T> : IEntity<T>
{
  public T Id { get; set; } = default!;
  public DateTime? CreatedAt { get; set; }
  public Guid? CreatedBy { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public Guid? UpdatedBy { get; set; }
}
