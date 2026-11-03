using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// The promises of the payment path, on a real PostgreSQL: its locks, unique indexes and transactions are the point.
[Collection(nameof(PaymentDatabase))]
public class GatewayPaymentTests(PaymentDatabase db)
{
  private static GatewayPaymentFacts Facts(string psid, decimal amount) =>
      new("PAYMENT", Guid.NewGuid().ToString(), PaymentChannel.Onelink, psid, amount, "TX-" + Guid.NewGuid().ToString("N")[..20], DateTime.UtcNow.AddMinutes(-1), "ABL");

  private Task<List<Payment>> PaymentsAsync(string psid) => db.QueryAsync(c => c.Payments.AsNoTracking().Include(p => p.Allocations).Where(p => p.Psid == psid).ToListAsync());

  private Task<GatewayNotification?> RowAsync(Guid? id) =>
      db.QueryAsync(c => c.GatewayNotifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == GatewayNotificationId.Of(id!.Value)));

  [Fact]
  public async Task A_genuine_report_settles_the_dues_and_leaves_its_record()
  {
    var psid = await db.FileOwingAsync(10000);

    var outcome = await db.ReportAsync(Facts(psid, 4000));

    Assert.Equal(GatewayOutcomeKind.Posted, outcome.Kind);
    Assert.Equal(6000, await db.DuesAsync(psid));
    var payment = Assert.Single(await PaymentsAsync(psid));
    Assert.Equal((4000m, 4000m, PaymentChannel.Onelink), (payment.Amount, payment.Allocations.Sum(a => a.Amount), payment.Channel));
    var row = await RowAsync(outcome.NotificationId);
    Assert.Equal(GatewayNotificationStatus.Posted, row!.Status);
    Assert.Equal(payment.Id, row.PaymentId);
    Assert.True(await db.QueryAsync(c => c.AuditTrail.AnyAsync(a => a.Action == "Posted" && a.RecordId == row.Id.Value)));
  }

  [Fact]
  public async Task A_forged_message_changes_nothing_and_is_not_stored()
  {
    var psid = await db.FileOwingAsync(10000);
    var before = await db.QueryAsync(c => c.GatewayNotifications.CountAsync());

    var outcome = await db.ReceiveAsync(new GatewayVerification.Refused("The signature does not match."));

    Assert.Equal(GatewayOutcomeKind.Refused, outcome.Kind);
    Assert.Equal(10000, await db.DuesAsync(psid));
    Assert.Equal(before, await db.QueryAsync(c => c.GatewayNotifications.CountAsync()));
  }

  [Fact]
  public async Task A_message_that_cannot_be_checked_is_not_treated_as_paid_or_as_failed()
  {
    var psid = await db.FileOwingAsync(10000);

    var outcome = await db.ReceiveAsync(new GatewayVerification.Unavailable("The gateway did not answer."));

    Assert.Equal(GatewayOutcomeKind.Unavailable, outcome.Kind);
    Assert.Equal(10000, await db.DuesAsync(psid));
    Assert.Empty(await PaymentsAsync(psid));
  }

  [Fact]
  public async Task A_repeated_report_changes_nothing()
  {
    var psid = await db.FileOwingAsync(10000);
    var facts = Facts(psid, 4000);

    var first = await db.ReportAsync(facts);
    var second = await db.ReportAsync(facts);

    Assert.Equal(GatewayOutcomeKind.AlreadyPosted, second.Kind);
    Assert.Equal(first.PaymentId, second.PaymentId);
    Assert.Single(await PaymentsAsync(psid));
    Assert.Equal(6000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task A_report_repeated_with_another_amount_is_refused()
  {
    var psid = await db.FileOwingAsync(10000);
    var facts = Facts(psid, 4000);
    await db.ReportAsync(facts);

    var outcome = await db.ReportAsync(facts with { Amount = 9000 });

    Assert.Equal(GatewayOutcomeKind.Refused, outcome.Kind);
    Assert.Single(await PaymentsAsync(psid));
    Assert.Equal(6000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task The_same_report_arriving_many_times_at_once_settles_once()
  {
    var psid = await db.FileOwingAsync(10000);
    var facts = Facts(psid, 4000);

    var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => db.ReportAsync(facts))));

    Assert.All(outcomes, o => Assert.Contains(o.Kind, new[] { GatewayOutcomeKind.Posted, GatewayOutcomeKind.AlreadyPosted }));
    Assert.Single(await PaymentsAsync(psid));
    Assert.Equal(6000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task Two_reports_of_one_transaction_settle_once()
  {
    var psid = await db.FileOwingAsync(10000);
    var facts = Facts(psid, 4000);
    await db.ReportAsync(facts);

    var outcome = await db.ReportAsync(facts with { EventId = Guid.NewGuid().ToString() });

    Assert.Equal(GatewayOutcomeKind.Posted, outcome.Kind);
    Assert.Single(await PaymentsAsync(psid));
    Assert.Equal(6000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task Different_payments_at_the_same_moment_cannot_pay_more_than_is_owed()
  {
    var psid = await db.FileOwingAsync(10000);

    var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => db.ReportAsync(Facts(psid, 4000)))));

    Assert.Equal(2, outcomes.Count(o => o.Kind == GatewayOutcomeKind.Posted));
    Assert.Equal(4, outcomes.Count(o => o.Kind == GatewayOutcomeKind.Held));
    Assert.Equal(2000, await db.DuesAsync(psid));
    Assert.Equal(2, (await PaymentsAsync(psid)).Count);
  }

  [Fact]
  public async Task Money_that_cannot_be_applied_is_held_then_applied_when_it_can_be()
  {
    var psid = await db.FileOwingAsync(3000);
    var facts = Facts(psid, 4000);

    var held = await db.ReportAsync(facts);

    Assert.Equal(GatewayOutcomeKind.Held, held.Kind);
    Assert.Contains("exceeds", held.Reason);
    Assert.Empty(await PaymentsAsync(psid));
    Assert.Equal(3000, await db.DuesAsync(psid));
    Assert.Equal(GatewayOutcomeKind.Held, (await db.ReportAsync(facts)).Kind);

    await db.InScopeAsync(async sp =>
    {
      var context = sp.GetRequiredService<IApplicationDbContext>();
      var file = await context.LoadTaxFileByPsidAsync(psid, default);
      var head = await context.TaxHeads.FirstAsync(h => h.Code == HeadCodes.LeaseMoney);
      context.ArrearsRows.Add(Arrears.Imported(ArrearsId.New(), file.Id, head, 2023, 1000, "Found later", null, Guid.NewGuid(), DateTime.UtcNow));
      await context.SaveChangesAsync(default);
    });

    var retried = await db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new RetryGatewayNotificationCommand(held.NotificationId!.Value)));

    Assert.Equal(GatewayOutcomeKind.Posted, retried.Value!.Outcome);
    Assert.Equal(0, await db.DuesAsync(psid));
    Assert.Single(await PaymentsAsync(psid));
  }

  [Fact]
  public async Task A_payment_on_an_unknown_reference_is_held_not_lost()
  {
    var outcome = await db.ReportAsync(Facts("999900000001", 4000));

    Assert.Equal(GatewayOutcomeKind.Held, outcome.Kind);
    var row = await RowAsync(outcome.NotificationId);
    Assert.Equal((GatewayNotificationStatus.Held, 4000m), (row!.Status, row.Amount));
    Assert.Contains("No property", row.Error);
  }

  [Fact]
  public async Task Part_of_a_rupee_is_held_rather_than_rounded_away()
  {
    var psid = await db.FileOwingAsync(10000);

    var outcome = await db.ReportAsync(Facts(psid, 4000.50m));

    Assert.Equal(GatewayOutcomeKind.Held, outcome.Kind);
    Assert.Equal(10000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task A_transaction_posted_on_another_file_is_held()
  {
    var first = await db.FileOwingAsync(10000);
    var second = await db.FileOwingAsync(10000);
    var facts = Facts(first, 4000);
    await db.ReportAsync(facts);

    var outcome = await db.ReportAsync(facts with { EventId = Guid.NewGuid().ToString(), Psid = second });

    Assert.Equal(GatewayOutcomeKind.Held, outcome.Kind);
    Assert.Equal(10000, await db.DuesAsync(second));
  }

  [Fact]
  public async Task A_report_left_half_way_is_finished_by_its_repeat()
  {
    var psid = await db.FileOwingAsync(10000);
    var facts = Facts(psid, 4000);
    await db.InScopeAsync(sp => sp.GetRequiredService<IApplicationDbContext>()
      .TryAddGatewayNotificationAsync(GatewayNotification.Record(GatewayNotificationId.New(), db.Gateway.Source, facts, DateTime.UtcNow), default));
    Assert.Empty(await PaymentsAsync(psid));

    var outcome = await db.ReportAsync(facts);

    Assert.Equal(GatewayOutcomeKind.Posted, outcome.Kind);
    Assert.Single(await PaymentsAsync(psid));
  }

  [Fact]
  public async Task A_dismissed_payment_stays_closed()
  {
    var psid = await db.FileOwingAsync(3000);
    var facts = Facts(psid, 4000);
    var held = await db.ReportAsync(facts);
    var id = held.NotificationId!.Value;

    await db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new DismissGatewayNotificationCommand(id, "Refunded by the bank")));

    var retried = await db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new RetryGatewayNotificationCommand(id)));
    Assert.False(retried.IsSuccess);
    Assert.NotEqual(GatewayOutcomeKind.Posted, (await db.ReportAsync(facts)).Kind);
    Assert.Empty(await PaymentsAsync(psid));
  }

  [Fact]
  public async Task Staff_cannot_post_by_hand_what_the_gateway_reports()
  {
    var psid = await db.FileOwingAsync(10000);

    await Assert.ThrowsAsync<DomainException>(() => db.InScopeAsync(sp => sp.GetRequiredService<ISender>()
      .Send(new ReceivePaymentCommand(psid, 4000, PaymentChannel.Onelink, "HAND-MADE", null, null, null))));

    Assert.Empty(await PaymentsAsync(psid));
    Assert.Equal(10000, await db.DuesAsync(psid));
  }

  [Fact]
  public async Task A_settlement_report_names_the_differences_and_settles_a_transaction_once()
  {
    var psid = await db.FileOwingAsync(10000);
    var agreed = Facts(psid, 3000);
    var differs = Facts(psid, 2000);
    var unsettled = Facts(psid, 1000);
    await db.ReportAsync(agreed);
    await db.ReportAsync(differs);
    await db.ReportAsync(unsettled);
    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
    ImportSettlementCommand Report(string batch, params SettlementLineInput[] lines) => new(PaymentChannel.Onelink, batch, today, today, today, lines);
    Task<Result<SettlementBatchDto>> ImportAsync(ImportSettlementCommand command) => db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(command));
    var batchRef = "B-" + Guid.NewGuid().ToString("N")[..12];

    var first = await ImportAsync(Report(batchRef, new SettlementLineInput(agreed.TransactionRef, psid, 3000, null), new SettlementLineInput(differs.TransactionRef, psid, 1500, null), new SettlementLineInput("NOT-OURS-" + batchRef, null, 700, null)));

    Assert.Equal((3, 1, 2), (first.Value!.LineCount, first.Value.MatchedCount, first.Value.ExceptionCount));
    Assert.Equal(4000, await db.DuesAsync(psid));
    var detail = await db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new GetSettlementQuery(first.Value.Id)));
    Assert.Contains(detail.Value!.Lines, l => l.Match == SettlementMatch.AmountDiffers && l.TransactionRef == differs.TransactionRef);
    Assert.Contains(detail.Value.Lines, l => l.Match == SettlementMatch.NotInErp);
    Assert.Contains(detail.Value.Unsettled, p => p.TransactionRef == unsettled.TransactionRef);
    Assert.DoesNotContain(detail.Value.Unsettled, p => p.TransactionRef == agreed.TransactionRef);

    await Assert.ThrowsAnyAsync<DbUpdateException>(() => ImportAsync(Report(batchRef, new SettlementLineInput(unsettled.TransactionRef, psid, 1000, null))));

    var later = await ImportAsync(Report(batchRef + "-2", new SettlementLineInput(agreed.TransactionRef, psid, 3000, null)));
    Assert.Equal((0, 1), (later.Value!.MatchedCount, later.Value.ExceptionCount));
  }
}
