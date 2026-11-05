/// A message exactly as a gateway sent it. The adapter alone reads it.
public sealed record GatewayMessage(string Body, IReadOnlyDictionary<string, string> Headers);

/// What the adapter found: only a Genuine message is ever acted on.
public abstract record GatewayVerification
{
  /// Authenticated as the gateway's specification requires and confirmed with the gateway; these facts can be trusted.
  public sealed record Genuine(GatewayPaymentFacts Facts) : GatewayVerification;

  /// Not proved to come from the gateway, or the gateway does not confirm it. Nothing is kept.
  public sealed record Refused(string Reason) : GatewayVerification;

  /// It could not be checked now (the gateway did not answer, nothing is configured). Not a failed payment: the outcome
  /// is unknown until it is checked again.
  public sealed record Unavailable(string Reason) : GatewayVerification;
}

/// A bank gateway. The adapter for a real gateway is written from its official specification, and is the only place that
/// knows its message formats, signatures and keys.
public interface IPaymentGateway
{
  /// Stored as the notification's source, and named as the actor in the audit trail.
  string Source { get; }

  /// The channels this gateway reports. Staff cannot post these by hand: the gateway is their only proof of money.
  IReadOnlySet<PaymentChannel> Channels { get; }

  /// Must authenticate the message exactly as the gateway's specification says, and confirm the payment independently
  /// where the gateway offers a way to. Must not throw for a bad message: answer Refused.
  Task<GatewayVerification> VerifyAsync(GatewayMessage message, CancellationToken cancellationToken);
}

public static class PaymentGatewayRules
{
  public static void EnsureHandPosted(this IPaymentGateway gateway, PaymentChannel channel)
  {
    if (gateway.Channels.Contains(channel))
      throw new DomainException($"{EnumText.Words(channel)} payments arrive from the bank and cannot be posted by hand.");
  }
}
