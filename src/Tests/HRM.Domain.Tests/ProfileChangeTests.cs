using static Fixture;

public class ProfileChangeTests
{
  private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
  private const string Contact = """{"contactType":"Mobile","value":"0300-1234567","isPrimary":true}""";

  private static ProfileChangeRequest Ask(ProfileChangeAction action = ProfileChangeAction.Add, Guid? target = null, string? proposed = Contact) =>
      ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), Employee(), ProfileChangeArea.Contact, action, target, proposed,
        "Add contact: Mobile 0300-1234567 (main)", null, null, null, null, Officer, Now);

  [Fact]
  public void A_change_waits_for_HR()
  {
    var change = Ask();
    Assert.Equal(ProfileChangeStatus.Pending, change.Status);
    Assert.True(change.IsPending);
  }

  [Fact]
  public void A_change_or_removal_names_the_entry_and_a_new_entry_does_not()
  {
    Assert.Throws<DomainException>(() => Ask(ProfileChangeAction.Change));
    Assert.Throws<DomainException>(() => Ask(ProfileChangeAction.Add, Guid.NewGuid()));
    Assert.Throws<DomainException>(() => Ask(ProfileChangeAction.Remove, Guid.NewGuid()));
    Assert.Equal(ProfileChangeAction.Remove, Ask(ProfileChangeAction.Remove, Guid.NewGuid(), null).Action);
  }

  [Fact]
  public void A_photo_is_only_replaced_and_needs_the_file()
  {
    var employee = Employee();
    Assert.Throws<DomainException>(() => ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), employee, ProfileChangeArea.Photo,
      ProfileChangeAction.Change, null, null, "New photo", null, null, null, null, Officer, Now));
    Assert.Throws<DomainException>(() => ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), employee, ProfileChangeArea.Photo,
      ProfileChangeAction.Add, null, null, "New photo", null, "/hrm-files/x/photo/a.jpg", null, null, Officer, Now));
    var photo = ProfileChangeRequest.Submit(ProfileChangeRequestId.New(), employee, ProfileChangeArea.Photo,
      ProfileChangeAction.Change, null, null, "New photo", null, "/hrm-files/x/photo/a.jpg", null, null, Officer, Now);
    Assert.Equal("/hrm-files/x/photo/a.jpg", photo.PhotoPath);
  }

  [Fact]
  public void A_rejection_says_why()
  {
    var change = Ask();
    Assert.Throws<DomainException>(() => change.Reject(Officer, Now, " "));
    change.Reject(Officer, Now, "The number is not yours - bring the SIM ownership slip.");
    Assert.Equal(ProfileChangeStatus.Rejected, change.Status);
    Assert.Equal(Officer, change.ReviewedBy);
  }

  [Fact]
  public void A_decided_change_is_final()
  {
    var change = Ask();
    change.Approve(Officer, Now, null);
    Assert.Throws<DomainException>(() => change.Withdraw(Now));
    Assert.Throws<DomainException>(() => change.Reject(Officer, Now, "late"));
  }

  [Fact]
  public void The_employee_withdraws_a_waiting_change()
  {
    var change = Ask();
    change.Withdraw(Now);
    Assert.Equal(ProfileChangeStatus.Withdrawn, change.Status);
    Assert.Throws<DomainException>(() => change.Approve(Officer, Now, null));
  }
}
