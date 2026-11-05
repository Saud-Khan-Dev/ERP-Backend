/// In place until a real gateway's adapter is written from its official specification: no message is ever trusted, and
/// staff keep posting bank payments by hand as before.
public sealed class NoPaymentGateway : IPaymentGateway
{
  public string Source => "none";

  public IReadOnlySet<PaymentChannel> Channels { get; } = new HashSet<PaymentChannel>();

  public Task<GatewayVerification> VerifyAsync(GatewayMessage message, CancellationToken cancellationToken) =>
      Task.FromResult<GatewayVerification>(new GatewayVerification.Unavailable("No payment gateway is configured."));
}
