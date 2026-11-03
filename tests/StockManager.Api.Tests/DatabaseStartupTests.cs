using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StockManager.Contracts;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Api.Tests;

/// <summary>
/// Integration tests of the database preparation at application startup.
/// Cover that an existing database keeps its data, that invalid seed files stop the startup, and that the real seed file is correct.
/// </summary>
public sealed class DatabaseStartupTests
{
    /// <summary>
    /// A quantity changed through the API is still there after a restart, because a database with data is not seeded again.
    /// </summary>
    [Fact]
    public async Task Restart_KeepsChangedQuantity()
    {
        using var database = new TestDatabase();

        // change a quantity in the first run
        using (var firstRun = database.StartHost())
        {
            var http = firstRun.CreateClient();
            var row = (await http.GetFromJsonAsync<List<StockRowDto>>("api/stock"))!.First();
            await http.PutAsJsonAsync($"api/stock/{row.ClientId}/{row.ProductId}", new UpdateStockRequest { Quantity = 1234 });
        }

        // read it in the second run
        using var secondRun = database.StartHost();
        var rows = await secondRun.CreateClient().GetFromJsonAsync<List<StockRowDto>>("api/stock");

        Assert.Contains(rows!, row => row.Quantity == 1234);
    }

    /// <summary>
    /// A seed file that writes to the generated code or refers to a code that does not exist stops the startup,
    /// with an exception that names the broken rule.
    /// </summary>
    [Theory]
    [InlineData("INSERT INTO Categories (Code, Name) VALUES ('KAT_000001', 'Tools');", "generated column")]
    [InlineData("""
        INSERT INTO Products (Id, Name) VALUES (1, 'Mouse');
        INSERT INTO CategoryProduct (CategoriesId, ProductsId)
        SELECT (SELECT Id FROM Categories WHERE Code = 'KAT_000099'), (SELECT Id FROM Products WHERE Code = 'PROD_000001');
        """, "NOT NULL")]
    public void InvalidSeed_StopsStartup(string seed, string expectedInMessage)
    {
        using var database = new TestDatabase(seed);
        using var host = database.StartHost();

        var exception = Assert.Throws<SqliteException>(() => host.CreateClient());

        Assert.Contains(expectedInMessage, exception.Message);
    }

    /// <summary>
    /// An error in a seed file leaves no part of the data in the database, so the next start with a correct file fills it completely.
    /// </summary>
    [Fact]
    public async Task FailedSeed_LeavesNoData()
    {
        using var database = new TestDatabase("INSERT INTO Categories (Id, Name) VALUES (1, 'Tools'), (2, 'Tools');");

        // the first start fails on the second category, which has the same name
        using (var firstRun = database.StartHost())
        {
            Assert.Throws<SqliteException>(() => firstRun.CreateClient());
        }

        // the second start uses a correct file
        database.ReplaceSeed(TestDatabase.DefaultSeed);
        using var secondRun = database.StartHost();
        var categories = await secondRun.CreateClient().GetFromJsonAsync<List<CategoryDto>>("api/categories");

        Assert.Equal(3, categories!.Count);
    }

    /// <summary>
    /// The real seed file of the application loads, and each stock row belongs to a product from the categories of its client,
    /// which the database does not check. A new record can be added after it.
    /// </summary>
    [Fact]
    public async Task RealSeed_HasOnlyStockFromClientCategories()
    {
        using var database = new TestDatabase(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "seed.sql")));
        using var host = database.StartHost();

        var rows = await host.CreateClient().GetFromJsonAsync<List<StockRowDto>>("api/stock");

        Assert.NotEmpty(rows!);
        Assert.All(rows!, row => Assert.NotEmpty(row.Categories));

        // add a new category and a new product
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(new Category { Name = "New category" }, new Product { Name = "New product" });
        await db.SaveChangesAsync();
    }
}
