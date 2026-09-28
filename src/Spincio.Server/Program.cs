using Spincio.Contracts;
using Spincio.Server.Hubs;
using Spincio.Server.Rooms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RoomOptions>(builder.Configuration.GetSection("Rooms"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomNotifier, HubRoomNotifier>();
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSignalR().AddJsonProtocol(o => SpincioJson.Configure(o.PayloadSerializerOptions));

// The PWA is served from another origin (e.g. GitHub Pages): allow the configured origins only.
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHub<GameHub>(HubPaths.Game);

app.Run();

/// <summary>Entry point, public for WebApplicationFactory in the integration tests.</summary>
public partial class Program;
