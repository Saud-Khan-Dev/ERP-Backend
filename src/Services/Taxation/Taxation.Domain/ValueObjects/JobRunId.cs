public sealed record JobRunId : ITypedId<JobRunId>
{
  public Guid Value { get; }

  private JobRunId(Guid value) => Value = value;

  public static JobRunId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Job run id cannot be empty.");

    return new JobRunId(value);
  }

  public static JobRunId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
