using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Spincio.Client;
using Spincio.Client.Game;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<ISavedGameStore, LocalStorageGameStore>();
builder.Services.AddSingleton(sp => new LocalGameSession(sp.GetRequiredService<ISavedGameStore>()));

await builder.Build().RunAsync();
