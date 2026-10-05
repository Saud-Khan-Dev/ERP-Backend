/// The sign-in security policy an administrator can edit in the app instead of in a configuration
/// file: what a password must contain, how lockout works, and how long a session lasts.
public class SecuritySettingsEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/security-settings", async (ISender sender) =>
    {
      var result = await sender.Send(new GetSecuritySettingsQuery());
      return Results.Ok(new GetSecuritySettingsResponse(result.Value!.Settings));
    })
      .RequirePermission(PermissionCatalog.Security.View)
      .WithName("GetSecuritySettings")
      .Produces<GetSecuritySettingsResponse>(StatusCodes.Status200OK)
      .WithSummary("Get Security Settings")
      .WithDescription("The current password rules, brute-force lockout and session lifetimes.");

    app.MapPut("/security-settings", async (UpdateSecuritySettingsRequest request, ISender sender) =>
    {
      var result = await sender.Send(new UpdateSecuritySettingsCommand(request.Settings));

      if (!result.IsSuccess)
        return Results.BadRequest(new { Message = result.Message });

      return Results.Ok(new UpdateSecuritySettingsResponse(result.Value!.Settings));
    })
      .RequirePermission(PermissionCatalog.Security.Edit)
      .WithName("UpdateSecuritySettings")
      .Produces<UpdateSecuritySettingsResponse>(StatusCodes.Status200OK)
      .ProducesProblem(StatusCodes.Status400BadRequest)
      .WithSummary("Update Security Settings")
      .WithDescription("Changes the password rules, lockout thresholds and session lifetimes. The new values apply from now on; tokens already issued keep their lifetime.");
  }
}

public sealed record GetSecuritySettingsResponse(SecuritySettingsDto Settings);

public sealed record UpdateSecuritySettingsRequest(SecuritySettingsInput Settings);
public sealed record UpdateSecuritySettingsResponse(SecuritySettingsDto Settings);
