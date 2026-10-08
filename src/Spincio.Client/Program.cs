using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Spincio.Client;
using Spincio.Client.Game;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<ISavedGameStore, LocalStorageGameStore>();
builder.Services.AddSingleton(sp => new LocalGameSession(sp.GetRequiredService<ISavedGameStore>()));
builder.Services.AddSingleton<GameHost>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<IOnlineSeatStore, LocalStorageOnlineSeatStore>();
builder.Services.AddSingleton<OnlineService>();
builder.Services.AddSingleton<StatsService>();

var host = builder.Build();
host.Services.GetRequiredService<StatsService>(); // starts following the matches from the first one
await host.RunAsync();
