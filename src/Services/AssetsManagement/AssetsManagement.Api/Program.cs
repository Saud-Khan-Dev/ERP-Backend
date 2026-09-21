var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationservices();
builder.Services.AddInfrastructureService(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

app.UseApiService();

app.Run();
