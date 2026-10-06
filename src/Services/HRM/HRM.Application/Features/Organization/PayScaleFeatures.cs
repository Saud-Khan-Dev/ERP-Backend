using FluentValidation;
using Microsoft.EntityFrameworkCore;

/// One grade's scale: min, max and either the stages or the annual increment to lay them out from.
public sealed record PayScaleInput(
  Guid GradeId,
  decimal MinBasicPay,
  decimal MaxBasicPay,
  decimal? AnnualIncrement,
  IReadOnlyList<PayScaleStageInput>? Stages,
  string? IncrementRule);

/// A notified pay scale (or a revision of all grades at once, e.g. "Revised Pay Scales 2025"). Each grade's scale in
/// force is closed the day before the new one starts.
public sealed record CreatePayScalesCommand(
  string? VersionLabel,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  IReadOnlyList<PayScaleInput> Scales) : ICommand<Result<CreatePayScalesCommandResult>>;

public sealed record CreatePayScalesCommandResult(IReadOnlyList<Guid> Ids);

public sealed record UpdatePayScaleCommand(Guid Id, string? IncrementRule, string? VersionLabel, string? NotificationRef) : ICommand<Result<UpdatedResult>>;

/// Replaces the stages of a scale nobody is paid on yet.
public sealed record ReplacePayScaleStagesCommand(Guid Id, decimal? AnnualIncrement, IReadOnlyList<PayScaleStageInput>? Stages) : ICommand<Result<UpdatedResult>>;

public sealed record SetPayScaleStatusCommand(Guid Id, RecordStatus Status) : ICommand<Result<UpdatedResult>>;

public sealed record GetPayScalesQueryResult(IReadOnlyList<PayScaleVersionDto> PayScales);

/// AsOf: only the scales in force that day.
public sealed record GetPayScalesQuery(Guid? GradeId, DateOnly? AsOf, bool IncludeInactive) : IQuery<Result<GetPayScalesQueryResult>>;

public sealed record GetPayScaleQueryResult(PayScaleVersionDto PayScale);

public sealed record GetPayScaleQuery(Guid Id) : IQuery<Result<GetPayScaleQueryResult>>;

public class PayScaleInputValidator : AbstractValidator<PayScaleInput>
{
  public PayScaleInputValidator()
  {
    RuleFor(x => x.GradeId).NotEmpty();
    RuleFor(x => x.MinBasicPay).GreaterThanOrEqualTo(0);
    RuleFor(x => x.MaxBasicPay).GreaterThanOrEqualTo(x => x.MinBasicPay).WithMessage("The maximum basic pay cannot be below the minimum.");
    RuleFor(x => x).Must(x => x.AnnualIncrement.HasValue != (x.Stages is { Count: > 0 }))
      .WithMessage("Give either the annual increment (the stages are laid out from it) or the stages, not both.");
    RuleFor(x => x.AnnualIncrement).GreaterThan(0).When(x => x.AnnualIncrement.HasValue);
    RuleFor(x => x.IncrementRule).MaximumLength(1000);
  }
}

public class CreatePayScalesCommandValidator : AbstractValidator<CreatePayScalesCommand>
{
  public CreatePayScalesCommandValidator()
  {
    RuleFor(x => x.VersionLabel).MaximumLength(100);
    RuleFor(x => x.NotificationRef).MaximumLength(200);
    RuleFor(x => x.Scales).NotEmpty();
    RuleForEach(x => x.Scales).SetValidator(new PayScaleInputValidator());
    RuleFor(x => x.Scales).Must(s => s.Select(x => x.GradeId).Distinct().Count() == s.Count)
      .WithMessage("Each grade may appear only once in a notification.");
  }
}

public class ReplacePayScaleStagesCommandValidator : AbstractValidator<ReplacePayScaleStagesCommand>
{
  public ReplacePayScaleStagesCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x).Must(x => x.AnnualIncrement.HasValue != (x.Stages is { Count: > 0 }))
      .WithMessage("Give either the annual increment or the stages, not both.");
  }
}

public class PayScaleHandlers(IApplicationDbContext context, ICurrentUser currentUser) :
  ICommandHandler<CreatePayScalesCommand, Result<CreatePayScalesCommandResult>>,
  ICommandHandler<UpdatePayScaleCommand, Result<UpdatedResult>>,
  ICommandHandler<ReplacePayScaleStagesCommand, Result<UpdatedResult>>,
  ICommandHandler<SetPayScaleStatusCommand, Result<UpdatedResult>>,
  IQueryHandler<GetPayScalesQuery, Result<GetPayScalesQueryResult>>,
  IQueryHandler<GetPayScaleQuery, Result<GetPayScaleQueryResult>>
{
  public async Task<Result<CreatePayScalesCommandResult>> Handle(CreatePayScalesCommand command, CancellationToken cancellationToken)
  {
    var gradeIds = command.Scales.Select(s => PayScaleGradeId.Of(s.GradeId)).ToList();
    var grades = await context.PayScaleGrades.Where(g => gradeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, cancellationToken);
    var existing = await context.PayScaleVersions.Include(v => v.Stages)
      .Where(v => gradeIds.Contains(v.GradeId) && v.Status == RecordStatus.Active).ToListAsync(cancellationToken);

    var created = new List<Guid>();
    foreach (var input in command.Scales)
    {
      var grade = grades.GetValueOrDefault(PayScaleGradeId.Of(input.GradeId))
        ?? throw new PayScaleGradeNotFoundException($"Grade {input.GradeId} was not found.");

      // the scale in force closes the day before the new one; a scale starting on or after it is a clash
      foreach (var current in existing.Where(v => v.GradeId == grade.Id && v.Range.Overlaps(command.EffectiveFrom, null)))
      {
        if (current.EffectiveFrom >= command.EffectiveFrom)
          return Result<CreatePayScalesCommandResult>.Failure($"{grade.Label} already has a scale from {current.EffectiveFrom:yyyy-MM-dd}; a new one must start after it.");
        current.CloseOn(command.EffectiveFrom.AddDays(-1));
      }

      var stages = input.AnnualIncrement is { } increment
        ? PayScaleVersion.StagesFromIncrement(input.MinBasicPay, input.MaxBasicPay, increment)
        : input.Stages!;
      var incrementRule = input.IncrementRule ?? (input.AnnualIncrement is { } step ? $"Annual increment Rs {step:N0}" : null);

      var version = PayScaleVersion.Create(PayScaleVersionId.New(), grade, input.MinBasicPay, input.MaxBasicPay, incrementRule,
        command.VersionLabel, command.NotificationRef, command.EffectiveFrom, null, stages, currentUser.UserId);
      context.PayScaleVersions.Add(version);
      created.Add(version.Id.Value);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<CreatePayScalesCommandResult>.Success(new(created));
  }

  public async Task<Result<UpdatedResult>> Handle(UpdatePayScaleCommand command, CancellationToken cancellationToken)
  {
    var version = await context.LoadPayScaleVersionAsync(command.Id, cancellationToken);
    version.UpdateDescription(command.IncrementRule, command.VersionLabel, command.NotificationRef);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ReplacePayScaleStagesCommand command, CancellationToken cancellationToken)
  {
    var version = await context.LoadPayScaleVersionAsync(command.Id, cancellationToken);
    if (await InUseAsync(version, cancellationToken))
      return Result<UpdatedResult>.Failure("Employees are already paid on this scale's stages. Notify a new scale instead of changing this one.");

    version.ReplaceStages(command.AnnualIncrement is { } increment
      ? PayScaleVersion.StagesFromIncrement(version.MinBasicPay, version.MaxBasicPay, increment)
      : command.Stages!);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(SetPayScaleStatusCommand command, CancellationToken cancellationToken)
  {
    var version = await context.LoadPayScaleVersionAsync(command.Id, cancellationToken);
    if (command.Status == RecordStatus.Active)
    {
      var clash = await context.PayScaleVersions.AnyAsync(v => v.Id != version.Id && v.GradeId == version.GradeId && v.Status == RecordStatus.Active
        && v.EffectiveFrom <= (version.EffectiveTo ?? DateOnly.MaxValue) && (v.EffectiveTo == null || v.EffectiveTo >= version.EffectiveFrom), cancellationToken);
      if (clash)
        return Result<UpdatedResult>.Failure("Another active scale of this grade covers part of the same period.");
    }

    version.SetStatus(command.Status);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<GetPayScalesQueryResult>> Handle(GetPayScalesQuery query, CancellationToken cancellationToken)
  {
    var versions = context.PayScaleVersions.AsNoTracking().Include(v => v.Stages).AsQueryable();
    if (query.GradeId is { } grade)
    {
      var gradeId = PayScaleGradeId.Of(grade);
      versions = versions.Where(v => v.GradeId == gradeId);
    }
    if (!query.IncludeInactive)
      versions = versions.Where(v => v.Status == RecordStatus.Active);
    if (query.AsOf is { } date)
      versions = versions.Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date));

    var rows = await versions.ToListAsync(cancellationToken);
    var grades = await context.PayScaleGrades.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.BpsNumber, cancellationToken);
    var stageIds = rows.SelectMany(v => v.Stages.Select(s => s.Id)).ToList();
    var used = (await context.PayRecords.AsNoTracking().Where(p => stageIds.Contains(p.PayScaleStageId)).Select(p => p.PayScaleStageId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

    var data = rows
      .OrderBy(v => grades.GetValueOrDefault(v.GradeId)).ThenByDescending(v => v.EffectiveFrom)
      .Select(v => v.ToDto(grades.GetValueOrDefault(v.GradeId), v.Stages.Any(s => used.Contains(s.Id))))
      .ToList();
    return Result<GetPayScalesQueryResult>.Success(new(data));
  }

  public async Task<Result<GetPayScaleQueryResult>> Handle(GetPayScaleQuery query, CancellationToken cancellationToken)
  {
    var versionId = PayScaleVersionId.Of(query.Id);
    var version = await context.PayScaleVersions.AsNoTracking().Include(v => v.Stages).FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
      ?? throw new PayScaleVersionNotFoundException($"Pay scale {query.Id} was not found.");
    var bps = await context.PayScaleGrades.AsNoTracking().Where(g => g.Id == version.GradeId).Select(g => g.BpsNumber).FirstAsync(cancellationToken);
    return Result<GetPayScaleQueryResult>.Success(new(version.ToDto(bps, await InUseAsync(version, cancellationToken))));
  }

  private async Task<bool> InUseAsync(PayScaleVersion version, CancellationToken cancellationToken)
  {
    var stageIds = version.Stages.Select(s => s.Id).ToList();
    return await context.PayRecords.AnyAsync(p => stageIds.Contains(p.PayScaleStageId), cancellationToken)
      || await context.PayrollSegments.AnyAsync(s => s.PayScaleStageId != null && stageIds.Contains(s.PayScaleStageId), cancellationToken);
  }
}
