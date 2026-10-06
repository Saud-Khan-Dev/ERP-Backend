public sealed record PostVersionId : ITypedId<PostVersionId>
{
  public Guid Value { get; }

  private PostVersionId(Guid value) => Value = value;

  public static PostVersionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Post version id cannot be empty.");

    return new PostVersionId(value);
  }

  public static PostVersionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
