using Kazo.Os.Ecosystem;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The whole OS comes from Kazo.Os.Ecosystem.Host; the apps are the packages referenced by the
// project, aggregated into this assembly by the build (KazoOsAppHost).
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.AddKazoOsBrowserHost(HostAppCatalog.Load(typeof(Program).Assembly));
await builder.Build().RunAsync();
