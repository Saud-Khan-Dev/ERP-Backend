using static Fixture;

public class GatewayAndSettlementTests
{
  private const string Source = "test-gateway";

  private static GatewayPaymentFacts Facts(decimal amount = 5000, string eventId = "EV-1", string reference = "TX-1", string psid = "6011 0000 0001") =>
      new("PAYMENT", eventId, PaymentChannel.Onelink, psid, amount, reference, Now.AddMinutes(-10), "ABL");

  private static GatewayNotification Report(GatewayPaymentFacts? facts = null) =>
      GatewayNotification.Record(GatewayNotificationId.New(), Source, facts ?? Facts(), Now);

  private static readonly DateOnly Day = D("2026-10-13");

  private static SettlementBatch Import(IReadOnlyList<SettlementLineInput> lines, IReadOnlyDictionary<string, SettledPayment>? payments = null, IReadOnlySet<string>? settledBefore = null) =>
      SettlementBatch.Import(SettlementBatchId.New(), PaymentChannel.Onelink, "B-1", Day, Day, Day, lines, payments ?? new Dictionary<string, SettledPayment>(),
        settledBefore ?? new HashSet<string>(), Now);

  private static SettledPayment Payment_(decimal amount = 5000, PaymentStatus status = PaymentStatus.Posted) => new(PaymentId.New(), amount, "601100000001", status);

  [Fact]
  public void A_report_is_kept_as_received_with_the_reference_in_digits()
  {
    var row = Report();
    Assert.Equal(GatewayNotificationStatus.Received, row.Status);
    Assert.Equal("601100000001", row.Psid);
    Assert.True(row.IsOpen);
  }

  [Theory]
  [InlineData(PaymentChannel.Cash)]
  [InlineData(PaymentChannel.BankCounter)]
  public void A_gateway_cannot_report_cash_or_counter_receipts(PaymentChannel channel) =>
      Assert.Throws<DomainException>(() => Report(Facts() with { Channel = channel }));

  [Theory]
  [InlineData(0)]
  [InlineData(-100)]
  public void A_report_needs_a_positive_amount(int amount) => Assert.Throws<DomainException>(() => Report(Facts(amount)));

  [Fact]
  public void A_report_needs_a_reference() => Assert.Throws<DomainException>(() => Report(Facts(psid: "no digits")));

  [Fact]
  public void A_repeat_must_say_what_the_first_report_said()
  {
    var row = Report();
    Assert.True(row.SameFacts(Source, Facts()));
    Assert.False(row.SameFacts(Source, Facts(amount: 9000)));
    Assert.False(row.SameFacts(Source, Facts(reference: "TX-2")));
    Assert.False(row.SameFacts(Source, Facts(psid: "601100000002")));
    Assert.False(row.SameFacts(Source, Facts() with { PaidAt = Now }));
    Assert.False(row.SameFacts("another-gateway", Facts()));
  }

  [Fact]
  public void A_repeat_compares_equal_with_the_time_as_the_database_keeps_it()
  {
    var precise = new DateTime(2026, 10, 13, 6, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
    var row = Report(Facts() with { PaidAt = precise });
    Assert.True(row.SameFacts(Source, Facts() with { PaidAt = precise }));
    Assert.Equal(0, row.PaidAt.Ticks % 10);
  }

  [Fact]
  public void A_report_needs_its_time_in_utc() =>
      Assert.Throws<DomainException>(() => Report(Facts() with { PaidAt = new DateTime(2026, 10, 13, 6, 0, 0, DateTimeKind.Unspecified) }));

  [Fact]
  public void A_held_report_can_still_be_posted_and_a_posted_one_cannot_change()
  {
    var row = Report();
    var file = TaxFileId.New();
    row.Hold("The amount exceeds the dues.", file, Now);
    Assert.Equal(GatewayNotificationStatus.Held, row.Status);
    Assert.True(row.IsOpen);

    var payment = PaymentId.New();
    row.MarkPosted(payment, file, Now);
    Assert.Equal(GatewayNotificationStatus.Posted, row.Status);
    Assert.Equal(payment, row.PaymentId);
    Assert.Null(row.Error);
    Assert.False(row.IsOpen);

    Assert.Throws<DomainException>(() => row.MarkPosted(PaymentId.New(), file, Now));
    Assert.Throws<DomainException>(() => row.Hold("again", file, Now));
    Assert.Throws<DomainException>(() => row.Dismiss("again", Now));
  }

  [Fact]
  public void A_dismissed_report_is_closed_and_needs_a_reason()
  {
    var row = Report();
    Assert.Throws<DomainException>(() => row.Dismiss(" ", Now));
    row.Dismiss("Refunded by the bank", Now);
    Assert.Equal(GatewayNotificationStatus.Dismissed, row.Status);
    Assert.Throws<DomainException>(() => row.MarkPosted(PaymentId.New(), TaxFileId.New(), Now));
  }

  [Fact]
  public void A_settlement_line_that_agrees_is_matched()
  {
    var payment = Payment_();
    var batch = Import([new("TX-1", "6011 0000 0001", 5000, null)], new Dictionary<string, SettledPayment> { ["TX-1"] = payment });
    var line = Assert.Single(batch.Lines);
    Assert.Equal(SettlementMatch.Matched, line.Match);
    Assert.Equal(payment.Id, line.PaymentId);
    Assert.Equal((1, 1, 0, 5000m), (batch.LineCount, batch.MatchedCount, batch.ExceptionCount, batch.TotalAmount));
  }

  [Fact]
  public void Each_difference_with_the_bank_is_named()
  {
    var payments = new Dictionary<string, SettledPayment>
    {
      ["AMOUNT"] = Payment_(4000), ["REVERSED"] = Payment_(status: PaymentStatus.Reversed), ["WRONG-REF"] = Payment_(), ["TWICE"] = Payment_()
    };
    var batch = Import([new("MISSING", null, 5000, null), new("AMOUNT", null, 5000, null), new("REVERSED", null, 5000, null), new("WRONG-REF", "601100000099", 5000, null),
      new("TWICE", null, 5000, null)], payments, new HashSet<string> { "TWICE" });

    SettlementMatch Of(string reference) => batch.Lines.Single(l => l.TransactionRef == reference).Match;
    Assert.Equal(SettlementMatch.NotInErp, Of("MISSING"));
    Assert.Equal(SettlementMatch.AmountDiffers, Of("AMOUNT"));
    Assert.Equal(SettlementMatch.ReversedInErp, Of("REVERSED"));
    Assert.Equal(SettlementMatch.ReferenceDiffers, Of("WRONG-REF"));
    Assert.Equal(SettlementMatch.AlreadySettled, Of("TWICE"));
    Assert.Equal((0, 5), (batch.MatchedCount, batch.ExceptionCount));
  }

  [Fact]
  public void A_report_cannot_list_a_transaction_twice() =>
      Assert.Throws<DomainException>(() => Import([new("TX-1", null, 5000, null), new(" TX-1 ", null, 5000, null)]));

  [Fact]
  public void A_report_needs_lines_a_real_period_and_bank_payments()
  {
    Assert.Throws<DomainException>(() => Import([]));
    Assert.Throws<DomainException>(() => SettlementBatch.Import(SettlementBatchId.New(), PaymentChannel.Onelink, "B", Day, Day, Day.AddDays(-1), [new("T", null, 1, null)],
      new Dictionary<string, SettledPayment>(), new HashSet<string>(), Now));
    Assert.Throws<DomainException>(() => SettlementBatch.Import(SettlementBatchId.New(), PaymentChannel.Cash, "B", Day, Day, Day, [new("T", null, 1, null)],
      new Dictionary<string, SettledPayment>(), new HashSet<string>(), Now));
  }

  [Fact]
  public void A_settlement_dated_after_today_is_refused()
  {
    var tomorrow = DateOnly.FromDateTime(Now.AddHours(5)).AddDays(1);
    Assert.Throws<DomainException>(() => SettlementBatch.Import(SettlementBatchId.New(), PaymentChannel.Onelink, "B-2", tomorrow, Day, tomorrow,
      [new("TX-1", null, 5000, null)], new Dictionary<string, SettledPayment>(), new HashSet<string>(), Now));
  }

  [Fact]
  public void A_line_time_without_a_zone_is_read_as_Pakistan_time()
  {
    var batch = Import([new("TX-1", null, 5000, new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Unspecified)), new("TX-2", null, 5000, new DateTime(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc))]);
    Assert.All(batch.Lines, l => Assert.Equal(DateTimeKind.Utc, l.PaidAt!.Value.Kind));
    Assert.Equal(new DateTime(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc), batch.Lines.Single(l => l.TransactionRef == "TX-1").PaidAt);
  }
}
