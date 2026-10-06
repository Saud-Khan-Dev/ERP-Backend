public sealed record TaskUpdateId : ITypedId<TaskUpdateId>
{
  public Guid Value { get; }

  private TaskUpdateId(Guid value) => Value = value;

  public static TaskUpdateId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Task update id cannot be empty.");

    return new TaskUpdateId(value);
  }

  public static TaskUpdateId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
