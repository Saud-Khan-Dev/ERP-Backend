using Microsoft.EntityFrameworkCore;

public sealed record DirectoryTownDto(Guid Id, string Code, string Name);

public sealed record DirectoryTownsResponse(IReadOnlyList<DirectoryTownDto> Towns);

/// Readers other modules use: the towns, for Administration's "where this person works" picker.
public class DirectoryEndpoints : ICarterModule
{
  public void AddRoutes(IEndpointRouteBuilder app)
  {
    app.MapGet("/directory/towns", async (IApplicationDbContext context, CancellationToken cancellationToken) =>
      {
        var towns = await context.Set<Town>().AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);
        return Results.Ok(new DirectoryTownsResponse(towns
          .OrderBy(t => t.SortOrder).ThenBy(t => t.Name.Value, StringComparer.OrdinalIgnoreCase)
          .Select(t => new DirectoryTownDto(t.Id.Value, t.Code.Value, t.Name.Value)).ToList()));
      })
      .RequireSignedIn()
      .WithTags("Directory").WithName("GetDirectoryTowns")
      .Produces<DirectoryTownsResponse>()
      .WithSummary("Towns")
      .WithDescription("Every active town (id, code, name) in the list's order - what Administration offers when it sets the towns a person works on. "
        + "Any signed-in person; not narrowed by the caller's own town scope.");
  }
}
