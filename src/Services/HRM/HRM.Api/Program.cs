var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// Apply migrations, then seed the catalogues, so a fresh clone runs without any manual `dotnet ef` commands.
// Auto-migration defaults to on in Development; set Database:AutoMigrate to control it explicitly.
await using (var scope = app.Services.CreateAsyncScope())
{
  var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
  await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync(autoMigrate);
}

app.UseApiService();

app.Run();
