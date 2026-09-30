using FluentValidation;

/// PROPERTY: prefix "PROP", separator "-", 5 digits, next number 1  →  PROP-00001.
public sealed record CodeSequenceInput(string Prefix, string? Separator, int MinimumDigits, long NextNumber);

public sealed record UpdateCodeSequenceCommandResult(CodeSequenceDto Sequence);

public sealed record UpdateCodeSequenceCommand(string Key, CodeSequenceInput Sequence) : ICommand<Result<UpdateCodeSequenceCommandResult>>;

public class UpdateCodeSequenceCommandValidator : AbstractValidator<UpdateCodeSequenceCommand>
{
  public UpdateCodeSequenceCommandValidator()
  {
    RuleFor(x => x.Key).NotEmpty();
    RuleFor(x => x.Sequence).NotNull();
    RuleFor(x => x.Sequence.Prefix).NotEmpty().MaximumLength(CodeSequence.MaxPrefixLength);
    RuleFor(x => x.Sequence.Separator).MaximumLength(1);
    RuleFor(x => x.Sequence.MinimumDigits).InclusiveBetween(1, CodeSequence.MaxMinimumDigits);
    RuleFor(x => x.Sequence.NextNumber).InclusiveBetween(1, CodeSequence.MaxNumber);
  }
}
