public sealed record PostId : ITypedId<PostId>
{
  public Guid Value { get; }

  private PostId(Guid value) => Value = value;

  public static PostId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Post id cannot be empty.");

    return new PostId(value);
  }

  public static PostId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
