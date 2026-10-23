/// The separation-of-duty rules (Administration › Configuration): every rule with its switch, the switch itself (Super
/// Administrator only) and the bare on/off states the other services read.
public class DutyRuleEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/duty-rules", async (ISender sender) =>
    {
      var result = await sender.Send(new GetDutyRulesQuery());
      return Results.Ok(new DutyRulesResponse(result.Value!));
    })
      .RequirePermission(PermissionCatalog.Security.View)
      .WithName("GetDutyRules")
      .Produces<DutyRulesResponse>(StatusCodes.Status200OK)
      .WithSummary("Separation-Of-Duty Rules")
      .WithDescription("Every rule: what it keeps apart, whether it is on, and who switched it last and why.");

    app.MapPut("/duty-rules/{code}", async (string code, SwitchDutyRuleRequest request, ISender sender) =>
    {
      var result = await sender.Send(new SwitchDutyRuleCommand(code, request.IsOn, request.Reason ?? string.Empty));
      return result.IsSuccess ? Results.Ok(new DutyRuleResponse(result.Value!)) : Results.BadRequest(new { result.Message });
    })
      .RequireAuthorization()
      .WithName("SwitchDutyRule")
      .Produces<DutyRuleResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Switch A Rule On Or Off")
      .WithDescription("Super Administrator only, with a reason (5-500 characters). Recorded in the activity trail.");

    // read by every service (a scheduled job has no token): codes and on/off only
    app.MapGet("/duty-rules/state", async (ISender sender) =>
    {
      var result = await sender.Send(new GetDutyRuleStatesQuery());
      return Results.Ok(new DutyRuleStatesResponse(result.Value!));
    })
      .AllowAnonymous()
      .WithName("GetDutyRuleStates")
      .Produces<DutyRuleStatesResponse>(StatusCodes.Status200OK)
      .WithSummary("Which Rules Are On")
      .WithDescription("Every rule's code and whether it is on - what the other services read, cached for a minute.");
  }
}

public sealed record DutyRulesResponse(IReadOnlyList<DutyRuleDto> Rules);
public sealed record DutyRuleResponse(DutyRuleDto Rule);
public sealed record SwitchDutyRuleRequest(bool IsOn, string? Reason);
