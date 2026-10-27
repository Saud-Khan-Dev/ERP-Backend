using static Fixture;

public class AnnouncementTests
{
  private static Announcement Post(string from = "2026-11-01", string? until = "2026-11-30") =>
      Announcement.Post(AnnouncementId.New(), "Office closed on 9 November", "Iqbal Day: offices stay closed.", null, D(from), until is null ? null : D(until), false);

  [Fact]
  public void It_shows_between_its_days_and_not_after_it_is_withdrawn()
  {
    var notice = Post();
    Assert.False(notice.ShowsOn(D("2026-10-31")));
    Assert.True(notice.ShowsOn(D("2026-11-01")));
    Assert.True(notice.ShowsOn(D("2026-11-30")));
    Assert.False(notice.ShowsOn(D("2026-12-01")));
    Assert.True(Post(until: null).ShowsOn(D("2030-01-01")));

    Assert.Throws<DomainException>(() => notice.Withdraw(Officer, DateTime.UtcNow, " "));
    notice.Withdraw(Officer, DateTime.UtcNow, "Posted by mistake");
    Assert.False(notice.ShowsOn(D("2026-11-10")));
    Assert.Throws<DomainException>(() => notice.Edit("x", "y", null, D("2026-11-01"), null, false));
  }

  [Fact]
  public void It_cannot_stop_before_it_starts_and_needs_a_title_and_a_message()
  {
    Assert.Throws<DomainException>(() => Post("2026-11-10", "2026-11-09"));
    Assert.Throws<DomainException>(() => Announcement.Post(AnnouncementId.New(), " ", "body", null, D("2026-11-01"), null, false));
    Assert.Throws<DomainException>(() => Announcement.Post(AnnouncementId.New(), "Title", " ", null, D("2026-11-01"), null, false));
  }
}
