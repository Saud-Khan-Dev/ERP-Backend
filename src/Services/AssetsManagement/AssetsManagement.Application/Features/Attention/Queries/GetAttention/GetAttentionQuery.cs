/// The Assets queues for the bell and the overview's "Needs attention", counted for the caller: only the queues the
/// caller may act on are listed (a zero count = checked, nothing waits). Every count is the register's own filter, so
/// the register opened with the same filters shows exactly that number.
///
/// Keys (the register filters each one stands for, always with disposed=exclude):
/// - assets.loans-overdue        loan=overdue                                  ASSETS.ASSIGN oldest = earliest return date missed
/// - assets.location-missing     missingRequiredLocation=true                 ASSETS.EDIT
/// - assets.out-of-service-long  outOfService=true&amp;inStatusOverDays=30         ASSETS.EDIT
/// - assets.warranty-ending      warrantyWithinDays=30                         ASSETS.EDIT   nextDue = nearest warranty end
/// - assets.depreciation-behind  depreciationBehind=true                       ASSET_FINANCE.POST (+ VIEW)
/// - assets.disposals-pending    disposed=proposed                             ASSET_FINANCE.APPROVE (+ VIEW)  oldest = first proposed
/// - assets.no-purchase          hasPurchase=false                             ASSET_FINANCE.CREATE (+ VIEW)
/// - assets.in-my-custody        custodianId=&lt;the caller's employee&gt;           ASSETS.VIEW and linked to an HRM employee; forMe
public sealed record GetAttentionQuery : IQuery<Result<AttentionResponse>>;
