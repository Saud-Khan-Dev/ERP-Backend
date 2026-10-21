public class DisposalAndStatusTests
{
  private static readonly DateOnly Today = new(2026, 10, 20);

  private static DisposalMethod Sale() => DisposalMethod.Create(DisposalMethodId.Of(Guid.NewGuid()), LookupCode.Of("SALE"), Name.Of("Sale"), requiresValue: true);
  private static DisposalMethod Scrap(bool allowsValue = true) => DisposalMethod.Create(DisposalMethodId.Of(Guid.NewGuid()), LookupCode.Of("SCRAP"), Name.Of("Scrapped"), requiresValue: false, allowsValue);

  private static AssetDisposal Dispose(DisposalMethod method, decimal? proceeds, decimal? bookValue, DateOnly? date = null, DateOnly? purchased = null) =>
    AssetDisposal.Create(AssetDisposalId.Of(Guid.NewGuid()), AssetId.Of(Guid.NewGuid()), method, date ?? Today, proceeds,
      proceeds is null ? null : Currency.Of("PKR"), Currency.Of("PKR"), purchased ?? new DateOnly(2024, 1, 1), bookValue, null, null,
      Fixture.Clerk, DateTime.UtcNow, Today);

  [Fact]
  public void Gain_or_loss_is_proceeds_less_book_value()
  {
    Assert.Equal(5_000m, Dispose(Sale(), 25_000m, 20_000m).GainLoss);
    Assert.Equal(-20_000m, Dispose(Scrap(), null, 20_000m).GainLoss);
    Assert.Equal(-15_000m, Dispose(Scrap(), 5_000m, 20_000m).GainLoss); // scrap sold for its metal
  }

  [Fact]
  public void Proceeds_follow_the_method()
  {
    Assert.Throws<DomainException>(() => Dispose(Sale(), null, 1m));
    Assert.Throws<DomainException>(() => Dispose(Scrap(allowsValue: false), 100m, 1m));
    Assert.Throws<DomainException>(() => Dispose(Sale(), -1m, 1m));
  }

  [Fact]
  public void Disposal_dates_must_be_real()
  {
    Assert.Throws<DomainException>(() => Dispose(Scrap(), null, 1m, date: Today.AddDays(1)));
    Assert.Throws<DomainException>(() => Dispose(Scrap(), null, 1m, date: new DateOnly(2023, 12, 31), purchased: new DateOnly(2024, 1, 1)));
    Assert.Equal("Clerk One", Dispose(Scrap(), null, 1m).ApprovedByName);
  }

  [Fact]
  public void A_final_status_is_reached_only_by_disposal_and_then_the_record_is_closed()
  {
    var (asset, type, inService) = Fixture.Asset();
    var disposed = Fixture.Status("DISPOSED", terminal: true);
    Assert.Throws<DomainException>(() => asset.ChangeStatus(inService, disposed));

    asset.Dispose(inService, disposed);
    Assert.Equal(disposed.Id, asset.StatusId);
    Assert.Throws<DomainException>(() => asset.UpdateDetails(Name.Of("x"), null, OwnershipType.Owned, disposed, null));
    Assert.Throws<DomainException>(() => asset.ChangeStatus(disposed, inService));
  }

  [Fact]
  public void A_cancelled_disposal_restores_the_earlier_status()
  {
    var (asset, _, inService) = Fixture.Asset();
    var disposed = Fixture.Status("DISPOSED", terminal: true);
    asset.Dispose(inService, disposed);
    asset.CancelDisposal(disposed, inService);
    Assert.Equal(inService.Id, asset.StatusId);
    Assert.Throws<DomainException>(() => asset.CancelDisposal(inService, inService));
  }

  [Fact]
  public void A_loan_can_always_come_back_even_when_the_status_no_longer_allows_moves()
  {
    var (asset, type, inService) = Fixture.Asset();
    var repair = Fixture.Status("UNDER_REPAIR", allowsAssignment: false);
    var person = new CustodyRef(Guid.NewGuid(), "Works", Guid.NewGuid(), "Ali Khan", "EMP-007");
    asset.AssignTo(inService, type, person, null);
    asset.ChangeStatus(inService, repair);

    Assert.Throws<DomainException>(() => asset.AssignTo(repair, type, CustodyRef.None, null));
    asset.ReturnFromLoan(repair, CustodyRef.None, null);
    Assert.Null(asset.CustodianId);
  }

  [Fact]
  public void Custody_keeps_the_names_shown_for_it()
  {
    var (asset, type, inService) = Fixture.Asset();
    asset.AssignTo(inService, type, new CustodyRef(Guid.NewGuid(), "Accounts", Guid.NewGuid(), "Sana Bibi", "EMP-012"), null);
    Assert.Equal("Sana Bibi", asset.CustodianName);
    Assert.Equal("EMP-012", asset.CustodianNumber);
    Assert.Equal("Accounts", asset.DepartmentName);
  }

  [Fact]
  public void Deleting_needs_a_reason()
  {
    var (asset, _, _) = Fixture.Asset();
    Assert.Throws<DomainException>(() => asset.SoftDelete("clerk", " ", DateTime.UtcNow));
    asset.SoftDelete("clerk", "Entered twice", DateTime.UtcNow);
    Assert.Equal("Entered twice", asset.DeletionReason);
    Assert.False(asset.IsActive);
  }

  [Fact]
  public void What_a_status_allows_is_locked_once_assets_use_it()
  {
    var status = Fixture.Status("IN_STORE");
    Assert.Throws<DomainException>(() => status.Update(status.Code, status.Name, null, isTerminal: true, allowsAssignment: false, null, 1, true, inUse: true));
    Assert.Throws<DomainException>(() => status.Update(status.Code, status.Name, null, isTerminal: false, allowsAssignment: false, null, 1, true, inUse: true));
    status.Update(status.Code, Name.Of("In the store"), null, false, true, null, 1, true, inUse: true);
    Assert.Equal("In the store", status.Name.Value);
  }

  [Fact]
  public void System_timeline_types_keep_their_code_stage_and_purpose()
  {
    var transfer = LifecycleEventType.CreateSystem(LifecycleEventTypeId.Of(Guid.NewGuid()), EventPurpose.Transfer, "ASSIGNMENT", LookupCode.Of("TRANSFERRED"), Name.Of("Transferred"), null);
    Assert.Throws<DomainException>(() => transfer.Update("ASSIGNMENT", LookupCode.Of("MOVED"), transfer.Name, null, true, false));
    Assert.Throws<DomainException>(() => transfer.Update("ASSIGNMENT", transfer.Code, transfer.Name, null, false, false));
    Assert.Throws<DomainException>(() => transfer.EnsureDeletable());
    transfer.Update("ASSIGNMENT", transfer.Code, Name.Of("Moved"), null, true, true);
    Assert.Equal("Moved", transfer.Name.Value);
    Assert.False(transfer.IsHandRecorded);
  }
}
