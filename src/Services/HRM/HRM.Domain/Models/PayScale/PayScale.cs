/// Pure job-title catalogue (Assistant Director, Sub Engineer, Driver ...). Independent of BPS, org unit and headcount:
/// a post puts a designation together with a grade and a unit.
public class Designation : Aggregate<DesignationId>
{
  public const int TitleMaxLength = 150;
  public const int CodeMaxLength = 30;

  public string Title { get; private set; } = default!;
  public string? Code { get; private set; }
  public string? Description { get; private set; }
  public bool IsActive { get; private set; }

  public static Designation Create(DesignationId id, string title, string? code, string? description)
  {
    var designation = new Designation { Id = id, IsActive = true };
    designation.Update(title, code, description);
    return designation;
  }

  public void Update(string title, string? code, string? description)
  {
    Title = Guard.RequiredText(title, TitleMaxLength, "Designation title");
    Code = Guard.OptionalCode(code, CodeMaxLength, "Designation code");
    Description = Guard.Text(description, 2000, "Description");
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Designation '{Title}' is inactive.");
  }
}

/// A Basic Pay Scale (BPS 1-22). The rows are seeded; only the name and the active flag change.
public class PayScaleGrade : Aggregate<PayScaleGradeId>
{
  public const int MinBps = 1;
  public const int MaxBps = 22;

  public int BpsNumber { get; private set; }
  public string? GradeName { get; private set; }
  public bool IsActive { get; private set; }

  public string Label => GradeName ?? $"BPS-{BpsNumber}";

  public static PayScaleGrade Create(PayScaleGradeId id, int bpsNumber, string? gradeName) => new()
  {
    Id = id,
    BpsNumber = Guard.Between(bpsNumber, MinBps, MaxBps, "BPS"),
    GradeName = Guard.Text(gradeName, 50, "Grade name"),
    IsActive = true
  };

  public void Update(string? gradeName, bool isActive)
  {
    GradeName = Guard.Text(gradeName, 50, "Grade name");
    IsActive = isActive;
  }

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"{Label} is inactive.");
  }
}

public sealed record PayScaleStageInput(int StageNumber, decimal BasicPay);

/// One notified revision of a grade's pay scale (e.g. Revised Pay Scales 2025 for BPS-17): minimum, maximum and the
/// annual-increment stages. A new notification is a new version; the previous one is closed the day before.
/// The stages are the single source of truth for stage amounts.
public class PayScaleVersion : Aggregate<PayScaleVersionId>
{
  private readonly List<PayScaleStage> _stages = new();

  public PayScaleGradeId GradeId { get; private set; } = default!;
  public decimal MinBasicPay { get; private set; }
  public decimal MaxBasicPay { get; private set; }
  public string? IncrementRule { get; private set; }
  public string? VersionLabel { get; private set; }
  public string? NotificationRef { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public RecordStatus Status { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  public IReadOnlyList<PayScaleStage> Stages => _stages.OrderBy(s => s.StageNumber).ToList().AsReadOnly();

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public static PayScaleVersion Create(
      PayScaleVersionId id,
      PayScaleGrade grade,
      decimal minBasicPay,
      decimal maxBasicPay,
      string? incrementRule,
      string? versionLabel,
      string? notificationRef,
      DateOnly effectiveFrom,
      DateOnly? effectiveTo,
      IReadOnlyCollection<PayScaleStageInput> stages,
      Guid? approvedBy)
  {
    ArgumentNullException.ThrowIfNull(grade);
    grade.EnsureActive();
    DateRange.EnsureValid(effectiveFrom, effectiveTo);

    var version = new PayScaleVersion
    {
      Id = id,
      GradeId = grade.Id,
      MinBasicPay = Guard.Money(minBasicPay, "Minimum basic pay"),
      MaxBasicPay = Guard.Money(maxBasicPay, "Maximum basic pay"),
      IncrementRule = Guard.Text(incrementRule, 1000, "Increment rule"),
      VersionLabel = Guard.Text(versionLabel, 100, "Version label"),
      NotificationRef = Guard.Text(notificationRef, 200, "Notification"),
      EffectiveFrom = effectiveFrom,
      EffectiveTo = effectiveTo,
      Status = RecordStatus.Active,
      ApprovedBy = approvedBy
    };

    if (version.MaxBasicPay < version.MinBasicPay)
      throw new DomainException("Maximum basic pay cannot be below the minimum.");

    version.ReplaceStages(stages);
    return version;
  }

  /// Stages 0..n from the minimum in steps of the annual increment, the last one capped at the maximum.
  public static IReadOnlyList<PayScaleStageInput> StagesFromIncrement(decimal minBasicPay, decimal maxBasicPay, decimal annualIncrement)
  {
    Guard.Positive(annualIncrement, "Annual increment");
    if (maxBasicPay < minBasicPay)
      throw new DomainException("Maximum basic pay cannot be below the minimum.");

    var stages = new List<PayScaleStageInput>();
    var pay = minBasicPay;
    for (var stage = 0; ; stage++)
    {
      stages.Add(new PayScaleStageInput(stage, Math.Min(pay, maxBasicPay)));
      if (pay >= maxBasicPay)
        break;
      if (stages.Count > 100)
        throw new DomainException("The increment is too small for this range: a scale cannot have more than 100 stages.");
      pay += annualIncrement;
    }

    return stages;
  }

  /// Stages may be replaced until an employee is paid on one of them (checked by the caller).
  public void ReplaceStages(IReadOnlyCollection<PayScaleStageInput> stages)
  {
    ArgumentNullException.ThrowIfNull(stages);

    if (stages.Count == 0)
      throw new DomainException("A pay scale needs at least one stage.");

    if (stages.Select(s => s.StageNumber).Distinct().Count() != stages.Count)
      throw new DomainException("Each stage number may appear only once.");

    var ordered = stages.OrderBy(s => s.StageNumber).ToList();
    decimal? previous = null;
    foreach (var stage in ordered)
    {
      if (stage.StageNumber < 0)
        throw new DomainException("Stage numbers start at 0.");

      var pay = Guard.Money(stage.BasicPay, $"Basic pay of stage {stage.StageNumber}");
      if (pay < MinBasicPay || pay > MaxBasicPay)
        throw new DomainException($"Stage {stage.StageNumber} ({pay:N2}) is outside the scale {MinBasicPay:N2} - {MaxBasicPay:N2}.");

      if (previous is { } before && pay < before)
        throw new DomainException($"Stage {stage.StageNumber} pays less than the stage before it.");

      previous = pay;
    }

    _stages.Clear();
    _stages.AddRange(ordered.Select(s => PayScaleStage.Create(Id, s.StageNumber, s.BasicPay)));
  }

  public PayScaleStage? Stage(int stageNumber) => _stages.FirstOrDefault(s => s.StageNumber == stageNumber);

  public PayScaleStage? Stage(PayScaleStageId stageId) => _stages.FirstOrDefault(s => s.Id == stageId);

  /// The next stage after the given one (an annual increment), or null at the top of the scale.
  public PayScaleStage? NextStage(int stageNumber) =>
      _stages.Where(s => s.StageNumber > stageNumber).OrderBy(s => s.StageNumber).FirstOrDefault();

  /// Pay fixation on moving into this scale: the lowest stage paying at least the given basic pay
  /// (or the top stage when the pay is above the scale).
  public PayScaleStage StageAtOrAbove(decimal basicPay) =>
      _stages.Where(s => s.BasicPay >= basicPay).OrderBy(s => s.StageNumber).FirstOrDefault()
      ?? _stages.OrderByDescending(s => s.StageNumber).First();

  /// Pay fixation on moving down: the highest stage paying at most the given basic pay (or stage 0).
  public PayScaleStage StageAtOrBelow(decimal basicPay) =>
      _stages.Where(s => s.BasicPay <= basicPay).OrderByDescending(s => s.StageNumber).FirstOrDefault()
      ?? _stages.OrderBy(s => s.StageNumber).First();

  public void CloseOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  /// An inactive version is ignored when finding the scale in force (and by the no-overlap rule).
  public void SetStatus(RecordStatus status) => Status = status;

  public void UpdateDescription(string? incrementRule, string? versionLabel, string? notificationRef)
  {
    IncrementRule = Guard.Text(incrementRule, 1000, "Increment rule");
    VersionLabel = Guard.Text(versionLabel, 100, "Version label");
    NotificationRef = Guard.Text(notificationRef, 200, "Notification");
  }
}

/// Annual-increment stage within a pay-scale version, e.g. BPS-17 stage 4 basic pay.
public class PayScaleStage : Entity<PayScaleStageId>
{
  public PayScaleVersionId PayScaleVersionId { get; private set; } = default!;
  public int StageNumber { get; private set; }
  public decimal BasicPay { get; private set; }

  internal static PayScaleStage Create(PayScaleVersionId versionId, int stageNumber, decimal basicPay) => new()
  {
    Id = PayScaleStageId.New(),
    PayScaleVersionId = versionId,
    StageNumber = Guard.NotNegative(stageNumber, "Stage number"),
    BasicPay = Guard.Money(basicPay, "Basic pay")
  };
}
