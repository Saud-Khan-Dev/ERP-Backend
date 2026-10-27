using static Fixture;

public class TaskTests
{
  private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
  private static readonly Guid Worker = Guid.Parse("22222222-2222-2222-2222-222222222222");

  private static EmployeeTask Task() => EmployeeTask.Assign(EmployeeTaskId.New(), Employee(), Officer, "Survey the Ayubia plots", null, TaskPriority.High, D("2026-11-05"));

  [Fact]
  public void A_task_reported_done_is_sent_back_open_again_with_what_is_left()
  {
    var task = Task();
    task.RecordProgress(100, "Done", Worker, Now);
    Assert.Equal(EmployeeTaskStatus.Completed, task.Status);

    task.SendBack(80, "Plot 7 is not measured yet", Officer, Now.AddHours(2));
    Assert.Equal(EmployeeTaskStatus.InProgress, task.Status);
    Assert.Equal(80, task.ProgressPercentage);
    Assert.Null(task.CompletedAt);
    Assert.Equal("Sent back: Plot 7 is not measured yet", task.Updates.Last().Notes);
  }

  [Fact]
  public void Only_a_task_reported_done_is_sent_back_and_only_with_a_reason_below_100()
  {
    var task = Task();
    Assert.Throws<DomainException>(() => task.SendBack(50, "not yet", Officer, Now));
    task.RecordProgress(100, null, Worker, Now);
    Assert.Throws<DomainException>(() => task.SendBack(50, " ", Officer, Now));
    Assert.Throws<DomainException>(() => task.SendBack(100, "redo", Officer, Now));
  }

  [Fact]
  public void A_file_goes_with_the_update_it_is_sent_with()
  {
    var task = Task();
    task.RecordProgress(40, "Half the plots", Worker, Now, "/hrm-files/x/tasks/a.jpg", "site.jpg");
    Assert.Equal("site.jpg", task.Updates.Single().FileName);
    Assert.Throws<DomainException>(() => task.RecordProgress(50, null, Worker, Now, "/hrm-files/x/tasks/b.jpg", null));
  }
}
