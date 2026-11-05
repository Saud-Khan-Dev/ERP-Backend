using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// The taxpayer portal (/my): a person sees what they pay on, and nothing of anyone else's.
[Collection(nameof(PaymentDatabase))]
public class TaxpayerPortalTests(PaymentDatabase db)
{
  private Task<Guid> FileIdAsync(string psid) => db.QueryAsync(c => c.TaxFiles.Where(f => f.Psid == psid).Select(f => f.Id.Value).FirstAsync());

  [Fact]
  public async Task A_taxpayer_sees_their_properties_with_what_is_due_and_how_to_pay_it()
  {
    var owner = Guid.NewGuid();
    var first = await db.FileOwingAsync(10000, owner);
    var second = await db.FileOwingAsync(2500, owner);

    var mine = (await db.AsTaxpayer(owner, s => s.Send(new GetMyTaxesQuery()))).Value!;

    Assert.True(mine.Linked);
    Assert.Equal("Asif Jadoon", mine.Name);
    Assert.Equal(12500, mine.TotalDue);
    Assert.Equal([first, second], mine.Files.Select(f => f.Psid).ToArray());

    var pay = (await db.AsTaxpayer(owner, s => s.Send(new GetMyPaymentDetailsQuery(mine.Files[0].TaxFileId)))).Value!;
    Assert.Equal((10000m, first), (pay.Amount, pay.Psid));
    Assert.StartsWith("000201", pay.QrPayload);
    Assert.Contains(first, pay.QrPayload);
  }

  [Fact]
  public async Task Nothing_of_another_taxpayers_is_found()
  {
    var me = Guid.NewGuid();
    await db.FileOwingAsync(10000, me);
    var theirs = await FileIdAsync(await db.FileOwingAsync(7000, Guid.NewGuid()));

    var mine = (await db.AsTaxpayer(me, s => s.Send(new GetMyTaxesQuery()))).Value!;
    Assert.DoesNotContain(mine.Files, f => f.TaxFileId == theirs);
    await Assert.ThrowsAsync<TaxFileNotFoundException>(() => db.AsTaxpayer(me, s => s.Send(new GetMyTaxFileQuery(theirs))));
    await Assert.ThrowsAsync<TaxFileNotFoundException>(() => db.AsTaxpayer(me, s => s.Send(new GetMyPaymentDetailsQuery(theirs))));
  }

  [Fact]
  public async Task An_account_that_is_not_a_taxpayers_has_nothing_to_see()
  {
    var none = await db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new GetMyTaxesQuery()));
    Assert.False(none.Value!.Linked);
    Assert.Empty(none.Value.Files);
    await Assert.ThrowsAsync<TaxFileNotFoundException>(() => db.InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new GetMyMessagesQuery(new PaginationRequest(0, 10)))));
  }

  [Fact]
  public async Task A_taxpayer_sees_their_payment_and_its_message_and_no_one_elses()
  {
    var owner = Guid.NewGuid();
    var psid = await db.FileOwingAsync(10000, owner);
    await db.ReportAsync(new("PAYMENT", Guid.NewGuid().ToString(), PaymentChannel.Onelink, psid, 4000, "TX-" + Guid.NewGuid().ToString("N")[..20], DateTime.UtcNow.AddMinutes(-1), "ABL"));
    var other = Guid.NewGuid();
    await db.ReportAsync(new("PAYMENT", Guid.NewGuid().ToString(), PaymentChannel.Onelink, await db.FileOwingAsync(9000, other), 1000,
      "TX-" + Guid.NewGuid().ToString("N")[..20], DateTime.UtcNow.AddMinutes(-1), "ABL"));

    var fileId = await FileIdAsync(psid);
    var file = (await db.AsTaxpayer(owner, s => s.Send(new GetMyTaxFileQuery(fileId)))).Value!;
    Assert.Equal(6000, file.File.Dues);
    Assert.Equal(4000, Assert.Single(file.Payments).Amount);

    var messages = (await db.AsTaxpayer(owner, s => s.Send(new GetMyMessagesQuery(new PaginationRequest(0, 10))))).Value!.Messages;
    Assert.Equal(1, messages.Count);
    Assert.Contains("4,000", Assert.Single(messages.Data).Body);
  }
}
