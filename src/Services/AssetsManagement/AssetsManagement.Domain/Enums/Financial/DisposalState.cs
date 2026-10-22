/// Where a disposal stands. A clerk proposes it (ASSET_FINANCE.CREATE); another person approves it
/// (ASSET_FINANCE.APPROVE) - only then does the asset leave the register - or rejects it; the proposer may withdraw it
/// while it waits. An approved disposal found to be a mistake is cancelled, never deleted: the row stays as the record
/// of what was done and undone.
public enum DisposalState
{
  Proposed,
  Approved,
  Rejected,
  Withdrawn,
  Cancelled
}
