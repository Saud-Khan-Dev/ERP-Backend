/// A notice HR posts in the portal: for everyone, or for one unit and the units under it. It shows from its first day
/// until its last (or until withdrawn); a pinned one stays on top. Whoever opens it is noted, so HR sees who has read it.
public class Announcement : Aggregate<AnnouncementId>
{
  public string Title { get; private set; } = default!;
  public string Body { get; private set; } = default!;
  /// The unit it is for, with the units under it; null = everyone.
  public OrganizationUnitId? OrgUnitId { get; private set; }
  public DateOnly ShowFrom { get; private set; }
  public DateOnly? ShowUntil { get; private set; }
  public bool IsPinned { get; private set; }
  /// A paper with it (a notification, the circular itself), stored on the file server.
  public string? FilePath { get; private set; }
  public string? FileName { get; private set; }
  public bool IsWithdrawn { get; private set; }
  public DateTime? WithdrawnAt { get; private set; }
  public Guid? WithdrawnBy { get; private set; }
  public string? WithdrawnReason { get; private set; }

  public static Announcement Post(AnnouncementId id, string title, string body, OrganizationUnitId? orgUnitId, DateOnly showFrom, DateOnly? showUntil, bool isPinned)
  {
    var announcement = new Announcement { Id = id };
    announcement.Edit(title, body, orgUnitId, showFrom, showUntil, isPinned);
    return announcement;
  }

  public void Edit(string title, string body, OrganizationUnitId? orgUnitId, DateOnly showFrom, DateOnly? showUntil, bool isPinned)
  {
    EnsureNotWithdrawn();
    if (showUntil is { } until && until < showFrom)
      throw new DomainException("It cannot stop showing before it starts.");
    Title = Guard.RequiredText(title, 200, "Title");
    Body = Guard.RequiredText(body, 4000, "Message");
    OrgUnitId = orgUnitId;
    ShowFrom = showFrom;
    ShowUntil = showUntil;
    IsPinned = isPinned;
  }

  public void Attach(string filePath, string fileName)
  {
    EnsureNotWithdrawn();
    FilePath = Guard.RequiredText(filePath, 500, "File");
    FileName = Guard.RequiredText(fileName, 255, "File name");
  }

  /// Taken down (posted by mistake, or no longer true): kept with who, when and why.
  public void Withdraw(Guid? by, DateTime at, string? reason)
  {
    EnsureNotWithdrawn();
    WithdrawnReason = Guard.Text(reason, 1000, "Reason") ?? throw new DomainException("Say why it is withdrawn.");
    WithdrawnBy = Guard.Actor(by, "withdraw an announcement");
    WithdrawnAt = at;
    IsWithdrawn = true;
  }

  public bool ShowsOn(DateOnly day) => !IsWithdrawn && ShowFrom <= day && (ShowUntil is null || ShowUntil >= day);

  private void EnsureNotWithdrawn()
  {
    if (IsWithdrawn)
      throw new DomainException("This announcement was withdrawn; post a new one instead.");
  }
}

/// That an employee opened an announcement (once).
public class AnnouncementRead : Entity<AnnouncementReadId>
{
  public AnnouncementId AnnouncementId { get; private set; } = default!;
  public EmployeeId EmployeeId { get; private set; } = default!;
  public DateTime ReadAt { get; private set; }

  public static AnnouncementRead Of(AnnouncementId announcementId, EmployeeId employeeId, DateTime at) =>
      new() { Id = AnnouncementReadId.New(), AnnouncementId = announcementId, EmployeeId = employeeId, ReadAt = at };
}
