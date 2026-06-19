using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Connect4HoopsArcade.Web;
using Connect4HoopsArcade.Web.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<GameSession>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Input.MoveRouter>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.SensorConnectionService>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.ISensorConnection>(
    sp => sp.GetRequiredService<Connect4HoopsArcade.Web.Services.SensorConnectionService>());
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.ISensorConnectionProxy>(
    sp => sp.GetRequiredService<Connect4HoopsArcade.Web.Services.SensorConnectionService>());
builder.Services.AddScoped<Connect4HoopsArcade.Web.Services.KeyboardInputService>();
// Interop services registered in later phases.

await builder.Build().RunAsync();
