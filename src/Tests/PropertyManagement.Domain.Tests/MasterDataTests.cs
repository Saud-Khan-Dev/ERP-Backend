using static Fixture;

public class MasterDataTests
{
  [Fact]
  public void Rule7_area_base_is_value_times_factor_to_base()
  {
    var kanal = Unit("KANAL", 5445m);

    Assert.Equal(10890m, kanal.ToBase(2));
  }

  [Fact]
  public void Base_unit_must_have_factor_one()
  {
    Assert.Throws<DomainException>(() => Unit("SQFT", 2m, isBase: true));
  }

  [Fact]
  public void Measurement_unit_needs_a_positive_factor()
  {
    Assert.Throws<DomainException>(() => Master<MeasurementUnit>("MARLA"));
  }

  [Fact]
  public void Master_code_is_normalized_to_upper_snake_case()
  {
    Assert.Equal("OPEN_LAND", MasterCode.Of("open land").Value);
  }

  [Theory]
  [InlineData(typeof(Town), 20, 150)]
  [InlineData(typeof(MeasurementUnit), 20, 50)]
  [InlineData(typeof(ContactType), 20, 50)]
  [InlineData(typeof(PropertyType), 30, 100)]
  public void Column_sizes_follow_the_erd(Type masterType, int code, int name)
  {
    var limits = MasterLimits.For(masterType);

    Assert.Equal((code, name), (limits.CodeLength, limits.NameLength));
  }

  [Fact]
  public void Town_code_longer_than_20_is_refused()
  {
    Assert.Throws<DomainException>(() => Master<Town>(new string('A', 21)));
  }

  [Fact]
  public void Rule9_inactive_master_cannot_be_chosen()
  {
    var town = Master<Town>("OLD_TOWN");
    town.Deactivate();

    Assert.Throws<DomainException>(() => town.EnsureActive());
  }

  [Fact]
  public void Workflow_statuses_are_system_codes()
  {
    Assert.True(SystemMasterCodes.IsRequired(typeof(RentalStatus), "CANCELLED"));
    Assert.True(SystemMasterCodes.IsRequired(typeof(MeasurementUnit), "SQFT"));
    Assert.False(SystemMasterCodes.IsRequired(typeof(PropertyType), "RESIDENTIAL"));
  }
}

public class CodeSequenceTests
{
  private static CodeSequence Property() =>
      CodeSequence.Create(CodeSequenceId.New(), MasterCode.Of("PROPERTY"), "PROP", "-", 5, 1);

  [Fact]
  public void Rule8_issues_padded_codes_in_order()
  {
    var sequence = Property();

    Assert.Equal("PROP-00001", sequence.Issue().Value);
    Assert.Equal("PROP-00002", sequence.Issue().Value);
    Assert.Equal(3, sequence.NextNumber);
  }

  [Fact]
  public void Numbers_grow_past_the_padding()
  {
    var sequence = CodeSequence.Create(CodeSequenceId.New(), MasterCode.Of("PROPERTY"), "BP", "-", 2, 100);

    Assert.Equal("BP-100", sequence.NextCode.Value);
  }

  [Theory]
  [InlineData("PROP-00012", true, 12)]
  [InlineData("PROP-0012", false, 0)]
  [InlineData("OWN-00012", false, 0)]
  public void Reads_only_canonical_codes(string code, bool matches, long number)
  {
    var ok = Property().TryReadNumber(BusinessCode.Of(code), out var read);

    Assert.Equal(matches, ok);
    Assert.Equal(number, read);
  }

  [Fact]
  public void Edited_template_changes_future_codes()
  {
    var sequence = Property();
    sequence.Update("GDA", "/", 4, 100);

    Assert.Equal("GDA/0100", sequence.NextCode.Value);
  }

  [Theory]
  [InlineData("1PROP", "-", 5, 1)]
  [InlineData("PROP", ".", 5, 1)]
  [InlineData("PROP", "-", 0, 1)]
  [InlineData("PROP", "-", 5, 0)]
  public void Invalid_templates_are_refused(string prefix, string separator, int digits, long next)
  {
    Assert.Throws<DomainException>(() => Property().Update(prefix, separator, digits, next));
  }
}

public class PropertyCoreTests
{
  [Fact]
  public void Rule2_status_change_closes_the_current_period_and_opens_a_new_one()
  {
    var property = Property();
    var opening = property.OpenStatusHistory(D("2026-01-01"), "registered");
    var encroached = Master<PropertyStatus>("ENCROACHED");

    var next = property.ChangeStatus(encroached, opening, D("2026-06-01"), "survey", null);

    Assert.Equal(D("2026-06-01"), opening.EffectiveTo);
    Assert.True(next.IsOpen);
    Assert.Equal(encroached.Id, property.PropertyStatusId);
  }

  [Fact]
  public void Status_cannot_start_before_the_current_period()
  {
    var property = Property();
    var opening = property.OpenStatusHistory(D("2026-06-01"), null);

    Assert.Throws<DomainException>(() =>
      property.ChangeStatus(Master<PropertyStatus>("VACANT"), opening, D("2026-05-01"), null, null));
  }

  [Fact]
  public void Changing_to_the_same_status_is_refused()
  {
    var current = Master<PropertyStatus>("OCCUPIED");
    var property = Property(status: current);

    Assert.Throws<DomainException>(() => property.ChangeStatus(current, null, D("2026-01-01"), null, null));
  }

  [Fact]
  public void Rule7_measurement_stores_square_feet_copies()
  {
    var measurement = PropertyMeasurement.Record(PropertyMeasurementId.New(), Property(), Unit("MARLA", 272.25m), 40, 20, null, null, null);

    Assert.Equal(10890m, measurement.TotalAreaBase);
    Assert.Equal(5445m, measurement.BuiltUpAreaBase);
    Assert.True(measurement.IsCurrent);
  }

  [Fact]
  public void Regularized_area_counts_only_while_in_force()
  {
    var regularization = PropertyAreaRegularization.Open(AreaRegularizationId.New(), Property(), Unit("SQFT", 1, true), 500,
      RegularizationStatus.Regularized, null, D("2026-02-01"), null, null, null, null);

    Assert.True(regularization.CountsTowardArea(D("2026-03-01")));
    Assert.False(regularization.CountsTowardArea(D("2026-01-01")));

    regularization.UpdateCase(RegularizationStatus.Regularized, D("2026-02-01"), null, null, null, D("2026-06-01"), null);
    Assert.False(regularization.CountsTowardArea(D("2026-07-01")));
  }

  [Fact]
  public void Regularized_case_needs_its_date()
  {
    Assert.Throws<DomainException>(() => PropertyAreaRegularization.Open(AreaRegularizationId.New(), Property(), Unit("SQFT", 1, true),
      500, RegularizationStatus.Regularized, null, null, null, null, null, null));
  }

  [Fact]
  public void Inactive_property_takes_no_new_records()
  {
    var property = Property();
    property.Deactivate();

    Assert.Throws<DomainException>(() =>
      PropertyMeasurement.Record(PropertyMeasurementId.New(), property, Unit("SQFT", 1, true), 10, 0, null, null, null));
  }
}
