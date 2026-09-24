var builder = WebApplication.CreateBuilder(args);

// Shared development credentials for the seeded Super Admin. Re-adding the environment variables
// afterwards keeps them the highest-priority source, so a real deployment can still override
// anything in seed.json with Seed__SuperAdminPassword.
builder.Configuration.AddJsonFile("seed.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// Apply migrations, then seed the permission catalogue, the protected SUPER_ADMIN role and the
// first administrator. Auto-migration defaults to on in Development so a fresh clone just runs;
// set Database:AutoMigrate to control it explicitly.
await using (var scope = app.Services.CreateAsyncScope())
{
  var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
  await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync(autoMigrate);
}

app.UseApiService();

app.Run();
