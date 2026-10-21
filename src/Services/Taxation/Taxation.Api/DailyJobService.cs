/// Taxation:DailyJob - Enabled (default true) and HourPkt (default 6): the hour, Pakistan time, from which the day's run starts.
public sealed record DailyJobOptions(bool Enabled, int HourPkt)
{
  public static DailyJobOptions From(IConfiguration configuration) => new(
    configuration.GetValue("Taxation:DailyJob:Enabled", true),
    Math.Clamp(configuration.GetValue("Taxation:DailyJob:HourPkt", 6), 0, 23));
}

/// Runs the daily job once a day (checked every 15 minutes): from the configured hour, when no run of the day finished.
public sealed class DailyJobService(DailyJob job, IConfiguration configuration, ILogger<DailyJobService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var options = DailyJobOptions.From(configuration);
    if (!options.Enabled)
    {
      logger.LogInformation("The taxation daily job is switched off (Taxation:DailyJob:Enabled).");
      return;
    }
    try
    {
      await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          if (await job.DueAsync(options.HourPkt, stoppingToken))
          {
            var run = await job.RunAsync(null, stoppingToken);
            logger.LogInformation("Taxation daily job {Status}: {Summary}{Error}", run.Status, run.Summary, run.Error);
          }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          logger.LogError(ex, "The taxation daily job could not be checked or run.");
        }
        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
      }
    }
    catch (OperationCanceledException)
    {
      // shutting down
    }
  }
}
