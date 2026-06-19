using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Connect4HoopsArcade.Web;
using Connect4HoopsArcade.Web.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<GameSession>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Input.MoveRouter>();
// Interop services registered in later phases.

await builder.Build().RunAsync();
