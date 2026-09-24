using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

// Apply migrations on startup so a fresh clone runs without any manual `dotnet ef` commands.
// Defaults to on in Development; set Database:AutoMigrate to control it explicitly.
if (app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment()))
{
  await using var scope = app.Services.CreateAsyncScope();
  await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}


app.UseApiService();

app.Run();
