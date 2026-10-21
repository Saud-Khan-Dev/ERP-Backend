/// The regular post an employee holds on the action's date.
public sealed record HeldPost(PostId PostId, string PostCode, int Bps, DateOnly Since);

/// The post an action moves the employee to, as it is on the action's date. SeatsTaken = the most regular holders it
/// has at any one time from that date on, the employee not counted.
public sealed record TargetPost(
  PostId PostId,
  string PostCode,
  int Bps,
  PostLifecycle Lifecycle,
  int Seats,
  int SeatsTaken,
  bool HasScaleInForce);

/// What the HR action rules need to know beyond the action itself (loaded by the caller for the action's date).
public sealed record HrActionFacts(
  Employee Employee,
  int RetirementAge,
  HeldPost? Current,
  TargetPost? Target,
  bool HasSeparation,
  bool SettlementPaid,
  ServiceEventType? EventType,
  bool HasUnpaidLeaveType,
  string? LeaveClash,
  bool HasPayBefore);

/// The rules of every HR action, in one place, so the dialog, the draft, the submit, the approval and the apply all
/// agree: whether the action fits the person's state on its date, the grade rule of a move, the free seat, the move
/// date, the pay scale in force, the age at appointment, and the extras each kind needs. Each refusal is one plain
/// sentence for the person at the desk.
public static class HrActionRules
{
  /// Refuses an action that cannot be carried out on its date. The facts are those of that date.
  public static void Check(HrActionDetails action, HrActionFacts facts)
  {
    if (Why(action, facts) is { } problem)
      throw new DomainException(problem);
  }

  /// Why the action cannot be carried out (null when it can).
  public static string? Why(HrActionDetails action, HrActionFacts facts)
  {
    ArgumentNullException.ThrowIfNull(action);
    ArgumentNullException.ThrowIfNull(facts);

    var e = facts.Employee;
    var who = e.DisplayName;
    var on = action.EffectiveDate;
    var type = action.ActionType;

    if (e.ProfileStatus != RecordStatus.Active)
      return $"The profile of {who} is inactive; reactivate it first.";

    if (Fit(type, facts) is { } unfit)
      return unfit;

    switch (type)
    {
      case HrActionType.Appointment:
      case HrActionType.DeputationIn:
        if (facts.Current is { } held)
          return $"{who} already holds post {held.PostCode} on {Day(on)}; use a transfer or promotion instead.";
        if (Post(facts, action, needsScale: true) is { } postProblem)
          return postProblem;
        if (e.AppointmentAgeProblem(on, facts.RetirementAge) is { } ageProblem)
          return ageProblem;
        if (type == HrActionType.DeputationIn && string.IsNullOrWhiteSpace(action.ExternalReferenceOrg))
          return "Name the parent department the deputationist comes from.";
        break;

      case HrActionType.Transfer:
      case HrActionType.Promotion:
      case HrActionType.Demotion:
        if (facts.Current is not { } from)
          return $"{who} holds no regular post on {Day(on)}, so there is nothing to {Verb(type)} from.";
        if (Post(facts, action, needsScale: type != HrActionType.Transfer) is { } moveProblem)
          return moveProblem;
        var to = facts.Target!;
        if (type == HrActionType.Promotion && to.Bps <= from.Bps)
          return $"A promotion needs a higher grade: post {to.PostCode} is BPS-{to.Bps}, the current post {from.PostCode} BPS-{from.Bps}.";
        if (type == HrActionType.Demotion && to.Bps >= from.Bps)
          return $"A demotion needs a lower grade: post {to.PostCode} is BPS-{to.Bps}, the current post {from.PostCode} BPS-{from.Bps}.";
        if (type == HrActionType.Transfer && to.Bps != from.Bps)
          return $"Post {to.PostCode} is BPS-{to.Bps} and the current post BPS-{from.Bps}; record a promotion or demotion instead of a transfer.";
        if (on <= from.Since)
          return $"{who} took the current post on {Day(from.Since)}; the move must be dated after that.";
        if (type != HrActionType.Transfer && !facts.HasPayBefore)
          return $"{who} has no pay record before {Day(on)} to fix the new pay from. Record the pay first.";
        break;

      case HrActionType.Joining:
        if (facts.Current is null)
        {
          if (action.NewPostId is null)
            return $"{who} holds no regular post on {Day(on)}; choose the post they join (the post kept on lien is suggested).";
          if (Post(facts, action, needsScale: true) is { } joinProblem)
            return joinProblem;
        }
        else if (action.NewPostId is not null)
        {
          return $"{who} still holds post {facts.Current.PostCode}; joining needs no new post.";
        }
        break;

      case HrActionType.DeputationOut:
        if (string.IsNullOrWhiteSpace(action.ExternalReferenceOrg))
          return "Name the department the employee is deputed to.";
        break;

      case HrActionType.Regularization:
        if (action.EmploymentMethod is null)
          return "Choose how the regularized employee is employed (scheduled seat or GDA personal).";
        break;

      case HrActionType.Lwop:
        if (action.EndDate is null)
          return "Give the last day of the leave without pay.";
        if (!facts.HasUnpaidLeaveType)
          return "There is no leave type that is deducted from pay. Add one under Configuration › Leave first.";
        if (facts.LeaveClash is { } clash)
          return clash;
        break;

      case HrActionType.Reinstatement:
        if (e.EmploymentStatus == EmploymentStatus.Suspended)
        {
          if (action.NewPostId is not null)
            return $"{who} kept the post during the suspension; a reinstatement after suspension needs no post.";
        }
        else
        {
          if (facts.SettlementPaid)
            return "The final settlement has been paid; record a fresh appointment instead of a reinstatement.";
          if (action.NewPostId is null)
            return $"Choose the post {who} is reinstated on.";
          if (Post(facts, action, needsScale: true) is { } reinstateProblem)
            return reinstateProblem;
        }
        break;

      case HrActionType.Other:
        if (facts.EventType is null)
          return "Choose the service event to record.";
        if (!facts.EventType.IsActive)
          return $"The event type '{facts.EventType.Name}' is inactive.";
        if (ServiceEventNames.IsSystem(facts.EventType.Name))
          return $"'{facts.EventType.Name}' has its own action; choose it instead.";
        break;
    }

    if (HrActionRequest.EndsService(type))
    {
      if (facts.HasSeparation)
        return $"{who}'s separation is already recorded.";
    }

    if (action.NewPostId is not null && !HrActionRequest.NeedsNewPost(type)
        && type is not (HrActionType.Joining or HrActionType.Reinstatement))
      return $"{Capitalize(EnumText.WithArticle(type))} does not move the employee to a post.";

    return null;
  }

  /// Whether an action of this kind fits the person's state at all (the dialog offers only these; the reason explains
  /// the ones it leaves out). The facts are those of the chosen date.
  public static string? Fit(HrActionType type, HrActionFacts facts)
  {
    var e = facts.Employee;
    var who = e.DisplayName;
    var status = e.EmploymentStatus;
    var state = EnumText.Words(status);

    if (status == EmploymentStatus.Deceased)
      return $"{who} is recorded as deceased.";

    if (type == HrActionType.Reinstatement)
    {
      return EmploymentStatuses.Reinstatable.Contains(status)
        ? status == EmploymentStatus.Suspended || facts.HasSeparation ? null : $"{who}'s separation record is missing."
        : $"Reinstatement brings back someone suspended, terminated, dismissed or removed from service; {who} is {state}.";
    }

    if (e.HasLeftService)
      return $"{who} has left service ({state}).";

    if (type == HrActionType.Death)
      return null;

    return type switch
    {
      HrActionType.Appointment or HrActionType.DeputationIn when status is EmploymentStatus.Suspended or EmploymentStatus.DeputedOut =>
        $"{who} is {state}; {(status == EmploymentStatus.DeputedOut ? "bring them back with a joining" : "reinstate them first")}.",
      HrActionType.Appointment or HrActionType.DeputationIn when facts.Current is { } held =>
        $"{who} already holds post {held.PostCode}.",
      HrActionType.Transfer or HrActionType.Promotion or HrActionType.Demotion when status is not (EmploymentStatus.Active or EmploymentStatus.OnLeave) =>
        $"{who} is {state}.",
      HrActionType.Transfer or HrActionType.Promotion or HrActionType.Demotion when facts.Current is null =>
        $"{who} holds no regular post to move from.",
      HrActionType.Joining when status is not (EmploymentStatus.DeputedOut or EmploymentStatus.OnLeave) =>
        $"Joining brings back an employee from deputation or leave; {who} is {state}.",
      HrActionType.DeputationIn when e.EmploymentType is not (EmploymentType.DeputationIn or EmploymentType.Regular) =>
        $"{who} is {EnumText.WithArticle(e.EmploymentType)} employee.",
      HrActionType.DeputationOut when status != EmploymentStatus.Active =>
        $"{who} is {state}; only an employee on duty is deputed out.",
      HrActionType.Regularization when e.EmploymentType == EmploymentType.Regular =>
        $"{who} is already a regular employee.",
      HrActionType.Regularization when e.EmploymentType == EmploymentType.DeputationIn =>
        $"{who} serves on deputation; a deputationist is absorbed by appointment, not regularized.",
      HrActionType.Lwop when status is not (EmploymentStatus.Active or EmploymentStatus.OnLeave) =>
        $"{who} is {state}.",
      HrActionType.Suspension when status is EmploymentStatus.Suspended =>
        $"{who} is already suspended.",
      HrActionType.Suspension when status is EmploymentStatus.DeputedOut =>
        $"{who} is on deputation; the borrowing department deals with discipline.",
      HrActionType.EndOfContract when e.EmploymentType is EmploymentType.Regular or EmploymentType.DeputationIn =>
        $"{who} is {EnumText.WithArticle(e.EmploymentType)} employee, not engaged on a contract.",
      HrActionType.DeputationEnd when e.EmploymentType != EmploymentType.DeputationIn =>
        $"{who} is not serving on deputation.",
      HrActionType.Retirement or HrActionType.CompulsoryRetirement or HrActionType.PrematureRetirement when e.EmploymentType != EmploymentType.Regular =>
        $"Only a regular employee retires; {who} is {EnumText.WithArticle(e.EmploymentType)} employee.",
      _ when HrActionRequest.EndsService(type) && facts.HasSeparation => $"{who}'s separation is already recorded.",
      _ => null
    };
  }

  private static string? Post(HrActionFacts facts, HrActionDetails action, bool needsScale)
  {
    if (action.NewPostId is null)
      return $"{Capitalize(EnumText.WithArticle(action.ActionType))} needs the new post.";
    if (facts.Target is not { } post || post.PostId != action.NewPostId)
      return $"The post does not exist on {Day(action.EffectiveDate)}.";
    if (post.Lifecycle != PostLifecycle.Sanctioned)
      return $"Post {post.PostCode} is {EnumText.Words(post.Lifecycle)} on {Day(action.EffectiveDate)} and takes no regular appointment.";
    if (post.SeatsTaken >= post.Seats)
      return $"Post {post.PostCode} is full: {post.SeatsTaken} of {post.Seats} seat(s) are held from {Day(action.EffectiveDate)}.";
    if (needsScale && !post.HasScaleInForce)
      return $"No pay scale of BPS-{post.Bps} is in force on {Day(action.EffectiveDate)}, so the pay cannot be fixed. Notify the scale first.";
    return null;
  }

  private static string Verb(HrActionType type) => type switch
  {
    HrActionType.Promotion => "promote",
    HrActionType.Demotion => "demote",
    _ => "transfer"
  };

  private static string Day(DateOnly date) => date.ToString("dd MMM yyyy");

  private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
