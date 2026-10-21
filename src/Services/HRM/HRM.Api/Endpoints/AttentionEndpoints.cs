/// The bell: what waits for the signed-in person in HRM (any signed-in user; only the queues they may act on).
public class AttentionEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/attention", async (ISender sender) => (await sender.Send(new GetAttentionQuery())).ToOk())
      .RequireSignedIn()
      .WithTags("Attention")
      .WithName("GetAttention")
      .Produces<AttentionResponse>()
      .WithSummary("What Waits for Me")
      .WithDescription("One exact count per queue the caller may act on (a zero means checked, nothing waits; a missing key means not theirs), "
        + "with the oldest waiting date or the next due date, and the caller's own work (forMe) when the account is linked to an employee. "
        + "Keys: docs/claude/hrm.md › Attention.");

    app.MapGet("/settings", async (ISender sender) => (await sender.Send(new GetHrmSettingsQuery())).ToOk())
      .RequireAnyPermission(HrmAuthorization.AnyHrmReader)
      .WithTags("Settings")
      .WithName("GetHrmSettings")
      .Produces<HrmSettingsDto>()
      .WithSummary("Rules in Force")
      .WithDescription("Maker-checker on HR actions and payroll, the retirement age and day, the annual increment date and qualifying months, and the PER score weights.");
  }
}
