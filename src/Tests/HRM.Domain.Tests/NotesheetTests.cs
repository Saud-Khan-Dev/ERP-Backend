using static Fixture;

public class NotesheetTests
{
  private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);

  private static (Notesheet Note, Employee Writer, Employee Officer, Employee Boss) Draft()
  {
    var writer = Employee(); var officer = Employee(); var boss = Employee();
    return (Notesheet.Write(NotesheetId.New(), 1, writer, "Purchase of two laptops", "Two laptops are needed for the Estate section."), writer, officer, boss);
  }

  [Fact]
  public void A_note_moves_up_and_is_decided_by_someone_other_than_its_writer()
  {
    var (note, writer, officer, boss) = Draft();
    Assert.Equal("NS-00001", note.Number);
    note.Send(writer.Id, officer, "For your kind approval", Now);
    Assert.Equal(officer.Id, note.HolderId);
    Assert.Throws<DomainException>(() => note.Decide(boss.Id, true, null, Now));
    note.Forward(officer.Id, boss, "Recommended", Now.AddHours(1));
    note.Decide(boss.Id, true, "Approved", Now.AddHours(2));
    Assert.Equal(NotesheetStatus.Approved, note.Status);
    Assert.Null(note.HolderId);
    Assert.Equal(3, note.Movements.Count);
    Assert.True(note.IsSeenBy(officer.Id) && note.IsSeenBy(boss.Id) && note.IsSeenBy(writer.Id));
    Assert.False(note.IsSeenBy(Employee().Id));
  }

  [Fact]
  public void Returned_to_its_writer_it_is_changed_and_sent_again()
  {
    var (note, writer, officer, _) = Draft();
    note.Send(writer.Id, officer, null, Now);
    Assert.Throws<DomainException>(() => note.Rewrite(writer.Id, "x", "y"));
    Assert.Throws<DomainException>(() => note.Return(officer.Id, " ", Now));
    note.Return(officer.Id, "Attach the quotation", Now.AddHours(1));
    Assert.Equal(NotesheetStatus.Returned, note.Status);
    note.Rewrite(writer.Id, "Purchase of two laptops", "Two laptops; quotation attached.");
    note.Send(writer.Id, officer, "Quotation attached", Now.AddHours(2));
    Assert.Equal(NotesheetStatus.InProgress, note.Status);
  }

  [Fact]
  public void A_rejection_says_why_and_a_writer_never_decides_their_own()
  {
    var (note, writer, officer, _) = Draft();
    note.Send(writer.Id, officer, null, Now);
    Assert.Throws<DomainException>(() => note.Decide(officer.Id, false, null, Now));
    note.Decide(officer.Id, false, "No budget this year", Now);
    Assert.Equal(NotesheetStatus.Rejected, note.Status);
    Assert.Throws<DomainException>(() => note.Withdraw(writer.Id, null, Now));
  }

  [Fact]
  public void Only_its_writer_withdraws_it_before_a_decision()
  {
    var (note, writer, officer, _) = Draft();
    note.Send(writer.Id, officer, null, Now);
    Assert.Throws<DomainException>(() => note.Withdraw(officer.Id, null, Now));
    note.Withdraw(writer.Id, "No longer needed", Now);
    Assert.Equal(NotesheetStatus.Withdrawn, note.Status);
    Assert.Throws<DomainException>(() => note.Forward(officer.Id, writer, null, Now));
  }
}
