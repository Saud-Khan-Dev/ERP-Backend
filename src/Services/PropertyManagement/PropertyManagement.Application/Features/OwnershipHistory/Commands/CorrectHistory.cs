using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record CorrectionResult(Guid Id);

/// An approving officer puts a registered transfer right as the old papers show it: its kind, date (and how well it is
/// known), deed / mutation no., price, relationship, approval order and remarks. Moving the date moves the holdings it
/// closed and opened with it. The reason is kept in the audit trail.
public sealed record CorrectTransferCommand(
  Guid Id,
  Guid TransferTypeId,
  DateOnly TransferDate,
  DatePrecision TransferDatePrecision,
  string? TransferReferenceNo,
  decimal? ConsiderationAmount,
  string? Relationship,
  string? ApprovalOrderRef,
  string? Remarks,
  string Reason) : ICommand<Result<CorrectionResult>>;

/// An approving officer puts a holding right as the old papers show it: how it was acquired, the deed / mutation no.,
/// remarks - and, for a holding no transfer or allotment opened (a first owner), when it started and how well that is known.
public sealed record CorrectOwnershipCommand(
  Guid Id,
  Guid? AcquisitionTransferTypeId,
  string? ReferenceNo,
  string? Remarks,
  DateOnly EffectiveFrom,
  DatePrecision EffectiveFromPrecision,
  string Reason) : ICommand<Result<CorrectionResult>>;

/// When and under what reference the property was first registered (null date: not known). Not after its first holding.
public sealed record SetOriginalRegistrationCommand(
  Guid PropertyId,
  DateOnly? RegisteredOn,
  DatePrecision RegisteredOnPrecision,
  string? RegistrationReference,
  string? Reason) : ICommand<Result<CorrectionResult>>;

public class CorrectTransferCommandValidator : AbstractValidator<CorrectTransferCommand>
{
  public CorrectTransferCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.TransferTypeId).NotEmpty();
    RuleFor(x => x.TransferDatePrecision).IsInEnum();
    RuleFor(x => x.TransferReferenceNo).MaximumLength(100);
    RuleFor(x => x.Relationship).MaximumLength(100);
    RuleFor(x => x.ApprovalOrderRef).MaximumLength(100);
    RuleFor(x => x.Remarks).MaximumLength(4000);
    RuleFor(x => x.ConsiderationAmount).GreaterThanOrEqualTo(0).When(x => x.ConsiderationAmount is not null);
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is corrected (e.g. the mutation register shows 1974, not 1975).").MaximumLength(1000);
  }
}

public class CorrectOwnershipCommandValidator : AbstractValidator<CorrectOwnershipCommand>
{
  public CorrectOwnershipCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.EffectiveFromPrecision).IsInEnum();
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Remarks).MaximumLength(4000);
    RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is corrected.").MaximumLength(1000);
  }
}

public class SetOriginalRegistrationCommandValidator : AbstractValidator<SetOriginalRegistrationCommand>
{
  public SetOriginalRegistrationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.RegisteredOnPrecision).IsInEnum();
    RuleFor(x => x.RegistrationReference).MaximumLength(100);
    RuleFor(x => x.Reason).MaximumLength(1000);
  }
}

public class CorrectHistoryHandler(IApplicationDbContext context, MasterLookup masters, IAuditTrail audit)
  : ICommandHandler<CorrectTransferCommand, Result<CorrectionResult>>,
    ICommandHandler<CorrectOwnershipCommand, Result<CorrectionResult>>,
    ICommandHandler<SetOriginalRegistrationCommand, Result<CorrectionResult>>
{
  public async Task<Result<CorrectionResult>> Handle(CorrectTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, transfer.Parties.Select(p => p.OwnerId), cancellationToken);

    var precision = command.TransferDatePrecision;
    transfer.CorrectHistory(
      await masters.GetAsync<TransferType>(command.TransferTypeId, cancellationToken), Normalize(command.TransferDate, precision), precision,
      command.TransferReferenceNo, command.ConsiderationAmount, command.Relationship, command.ApprovalOrderRef, command.Remarks, record);

    audit.Record(PropertyAuditActions.Corrected, transfer, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<CorrectionResult>.Success(new CorrectionResult(transfer.Id.Value));
  }

  public async Task<Result<CorrectionResult>> Handle(CorrectOwnershipCommand command, CancellationToken cancellationToken)
  {
    var row = await context.Ownerships.FirstOrDefaultAsync(o => o.Id == OwnershipId.Of(command.Id), cancellationToken)
      ?? throw new OwnershipNotFoundException($"Ownership {command.Id} was not found.");
    var property = await context.LoadPropertyAsync(row.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, [row.OwnerId], cancellationToken);
    var tracked = record.Ownerships.Single(o => o.Id == row.Id);

    var precision = command.EffectiveFromPrecision;
    tracked.CorrectHistory(
      await masters.GetOptionalAsync<TransferType>(command.AcquisitionTransferTypeId, cancellationToken),
      command.ReferenceNo, command.Remarks, Normalize(command.EffectiveFrom, precision), precision, record);

    audit.Record(PropertyAuditActions.Corrected, tracked, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<CorrectionResult>.Success(new CorrectionResult(tracked.Id.Value));
  }

  public async Task<Result<CorrectionResult>> Handle(SetOriginalRegistrationCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var firstHolding = await context.Ownerships.AsNoTracking().Where(o => o.PropertyId == property.Id)
      .OrderBy(o => o.EffectiveFrom).Select(o => (DateOnly?)o.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);

    var precision = command.RegisteredOnPrecision;
    property.SetOriginalRegistration(
      command.RegisteredOn is { } on ? Normalize(on, precision) : null, precision, command.RegistrationReference,
      firstHolding, PropertyWriteGuard.Today());

    audit.Record(PropertyAuditActions.Corrected, property, string.IsNullOrWhiteSpace(command.Reason) ? "Original registration recorded" : command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<CorrectionResult>.Success(new CorrectionResult(property.Id.Value));
  }

  /// A date known only to the month or year is kept as the first day of it.
  private static DateOnly Normalize(DateOnly date, DatePrecision precision) => precision switch
  {
    DatePrecision.Month => new DateOnly(date.Year, date.Month, 1),
    DatePrecision.Year => new DateOnly(date.Year, 1, 1),
    _ => date,
  };
}
