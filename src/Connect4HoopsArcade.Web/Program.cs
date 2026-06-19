using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.IAudioService,
                              Connect4HoopsArcade.Web.Services.AudioService>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.NarratorService>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore,
                              Connect4HoopsArcade.Web.Services.SettingsStore>();
// Interop services registered in later phases.

var host = builder.Build();
host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.NarratorService>(); // eager: wire event subscriptions
await host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore>().LoadAsync();
await host.RunAsync();
