using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StockManager.Web;
using StockManager.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// the root component selects the page for the current address
builder.RootComponents.Add<App>("#app");

// the REST API runs on the same address as the client
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<StockApiClient>();

await builder.Build().RunAsync();
