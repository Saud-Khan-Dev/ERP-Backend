/// The Property module's own verbs in the audit trail (besides the shared Added / Changed / Removed / Exported ...).
/// A decision writes one line with its verb and the reason or order in the detail, next to the field lines the save
/// writes on its own - so "who approved TRF-00012, under which order" is one line to find.
public static class PropertyAuditActions
{
  public const string Approved = "Approved";
  public const string Rejected = "Rejected";
  public const string Cancelled = "Cancelled";
  /// A past transfer entered while the PROPERTY_TRANSFER rule is on: it waits for a second officer.
  public const string RecordedForApproval = "Recorded for approval";
  public const string Registered = "Registered";
  /// An ownership history entry taken back by the clerk before a decision.
  public const string Withdrawn = "Withdrawn";
  /// A registered transfer, a holding or the original registration put right from the old papers (the reason in the detail).
  public const string Corrected = "Corrected";
  public const string Allotted = "Allotted";
  public const string Restored = "Restored";
  public const string Confirmed = "Confirmed";
  public const string Terminated = "Terminated";
  public const string Released = "Released";
  public const string Enforced = "Enforced";
  public const string Decided = "Decided";
  public const string Resolved = "Resolved";
  public const string Awarded = "Awarded";
  public const string Imposed = "Imposed";
  public const string Waived = "Waived";
  public const string SentForRecovery = "Sent for recovery";
  public const string Deactivated = "Deactivated";
  public const string Activated = "Activated";
}
