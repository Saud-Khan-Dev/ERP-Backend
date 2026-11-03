// ---- bank gateways and settlement ----

/// A payment a gateway reported. Error: why it is held, or why the Section dismissed it.
public sealed record GatewayNotificationDto(Guid Id, string Source, string EventType, string EventId, PaymentChannel Channel, string Psid, decimal Amount, string TransactionRef,
  string? BankCode, DateTime PaidAt, DateTime ReceivedAt, DateTime? ProcessedAt, GatewayNotificationStatus Status, Guid? TaxFileId, string? FileNo, Guid? PaymentId, string? Error);

public sealed record SettlementBatchDto(Guid Id, PaymentChannel Channel, string BatchRef, DateOnly SettlementDate, DateOnly CoversFrom, DateOnly CoversTo, DateTime ImportedAt,
  decimal TotalAmount, int LineCount, int MatchedCount, int ExceptionCount);

public sealed record SettlementLineDto(Guid Id, string TransactionRef, string? Psid, decimal Amount, DateTime? PaidAt, SettlementMatch Match, Guid? PaymentId, string? Note);

/// A payment of the report's period and channel that no report has settled: the bank has not confirmed this money.
public sealed record UnsettledPaymentDto(Guid PaymentId, string? TransactionRef, string? Psid, decimal Amount, DateTime PaidAt);
