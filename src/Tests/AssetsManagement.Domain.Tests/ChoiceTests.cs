public class ChoiceTests
{
  private static OptionSet Os()
  {
    var set = OptionSet.Create(OptionSetId.Of(Guid.NewGuid()), LookupCode.Of("OS"), Name.Of("Operating system"), null, false);
    set.AddValue(OptionSetValueId.Of(Guid.NewGuid()), null, LookupCode.Of("WIN10"), "Windows 10", null, null, 1);
    set.AddValue(OptionSetValueId.Of(Guid.NewGuid()), null, LookupCode.Of("WIN11"), "Windows 11", null, null, 2);
    return set;
  }

  [Fact]
  public void A_switched_off_choice_stays_valid_for_assets_that_hold_it_but_is_not_offered()
  {
    var set = Os();
    var win10 = set.Values.Single(v => v.Value.Value == "WIN10");
    set.UpdateValue(win10.Id, null, win10.Value, win10.Label, null, null, 1, isActive: false);

    Assert.DoesNotContain(set.Choices([]), c => c.Code == "WIN10");
    var kept = set.Choices(["WIN10"]).Single(c => c.Code == "WIN10");
    Assert.False(kept.IsOffered);
  }

  [Fact]
  public void Renaming_a_choice_keeps_its_stored_value()
  {
    var set = Os();
    var win11 = set.Values.Single(v => v.Value.Value == "WIN11");
    set.UpdateValue(win11.Id, null, win11.Value, "Windows 11 Pro", null, null, 2, true);
    Assert.Equal("Windows 11 Pro", set.FindByCode("WIN11")!.Label);
  }

  [Fact]
  public void Two_choices_cannot_share_a_name()
  {
    var set = Os();
    Assert.Throws<DomainException>(() => set.AddValue(OptionSetValueId.Of(Guid.NewGuid()), null, LookupCode.Of("W11"), "windows 11", null, null, 3));
  }
}
