/// The id of what a command created.
public sealed record CreatedResult(Guid Id);

/// A command that changed something and has nothing else to say.
public sealed record UpdatedResult(bool IsSuccess)
{
  public static readonly UpdatedResult Done = new(true);
}

public static class CommandResults
{
  public static Result<CreatedResult> Created(ITypedId id) => Result<CreatedResult>.Success(new CreatedResult(id.Value));

  public static Result<UpdatedResult> Updated() => Result<UpdatedResult>.Success(UpdatedResult.Done);
}
