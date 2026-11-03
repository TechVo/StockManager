using Microsoft.EntityFrameworkCore;
using StockManager.Server.Data;
using StockManager.Server.Endpoints;
using StockManager.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// register problem details responses, request validation, the database and the stock service
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Stock")));
builder.Services.AddScoped<StockService>();

var app = builder.Build();

// create the database and fill it with test data if it is empty
await app.InitializeDatabaseAsync(reset: args.Contains("--reset-db"));

// serve the Blazor WebAssembly client, static assets because the framework loads files with fingerprinted names
app.MapStaticAssets();
app.MapFallbackToFile("/", "index.html");
app.MapFallbackToFile("/detail/{code}", "index.html");

// expose the REST API
app.MapStockEndpoints();

app.Run();
