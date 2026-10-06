using static Fixture;

public class CommonTests
{
  [Theory]
  [InlineData("3520212345671")]
  [InlineData("35202-1234567-1")]
  [InlineData(" 35202 1234567 1 ")]
  public void A_cnic_is_stored_with_dashes(string typed) => Assert.Equal("35202-1234567-1", Cnic.Normalize(typed));

  [Theory]
  [InlineData("352021234567")]
  [InlineData("35202-1234567-X")]
  [InlineData("")]
  public void A_cnic_needs_13_digits(string typed) => Assert.Throws<DomainException>(() => Cnic.Normalize(typed));

  [Fact]
  public void An_iban_is_checked_and_stored_without_spaces()
  {
    Assert.Equal("PK36SCBL0000001123456702", Iban.NormalizeOptional("pk36 scbl 0000 0011 2345 6702"));
    Assert.Null(Iban.NormalizeOptional("  "));
  }

  [Theory]
  [InlineData("PK00SCBL0000001123456702")]
  [InlineData("PK36SCBL000000112345670")]
  [InlineData("GB36SCBL0000001123456702")]
  [InlineData("PK3612340000001123456702")]
  public void A_wrong_iban_is_refused(string typed) => Assert.Throws<DomainException>(() => Iban.NormalizeOptional(typed));

  [Fact]
  public void Date_ranges_overlap_and_clip_inclusively()
  {
    var range = new DateRange(D("2026-01-01"), D("2026-01-31"));

    Assert.True(range.Overlaps(D("2026-01-31"), null));
    Assert.False(range.Overlaps(D("2026-02-01"), null));
    Assert.Equal(new DateRange(D("2026-01-20"), D("2026-01-31")), range.Clip(D("2026-01-20"), D("2026-02-10")));
    Assert.Null(range.Clip(D("2026-02-01"), D("2026-02-10")));
    Assert.Equal(31, range.Days);
  }

  [Fact]
  public void Enum_names_read_as_words_in_messages()
  {
    Assert.Equal("deputation in", EnumText.Words(HrActionType.DeputationIn));
    Assert.Equal("an approved", EnumText.WithArticle(LeaveStatus.Approved));
    Assert.Equal("An arrears", EnumText.WithArticle(PayrollRunType.Arrears, capitalized: true));
    Assert.Equal("Final settlement", EnumText.Label(PayrollRunType.FinalSettlement));
  }

  [Fact]
  public void A_regular_employee_needs_an_employment_method()
  {
    Assert.Throws<DomainException>(() => Employee.Create(EmployeeId.New(), "EMP-900",
      new PersonalDetails("A", null, "B", "3520299999991", D("1990-01-01"), null, null, null, null), EmploymentType.Regular, null, null, D("2026-10-06")));
  }

  [Fact]
  public void Superannuation_is_the_sixtieth_birthday()
  {
    var employee = Fixture.Employee(D("1966-10-20"));

    Assert.Equal(D("2026-10-20"), employee.SuperannuationDate(60));
  }
}
