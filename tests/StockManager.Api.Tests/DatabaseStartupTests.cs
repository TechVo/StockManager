using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StockManager.Contracts;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Api.Tests;

/// <summary>
/// Integration tests of the database preparation at application startup.
/// Cover that an existing database keeps its data and that invalid seed files stop the startup.
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
    /// A category and a product keep the codes from the seed file, whatever their position in the file is,
    /// and other records refer to them by these codes. A new record then gets the number after the highest one.
    /// </summary>
    [Fact]
    public async Task SeedCodes_AreKeptAndNextCodeFollowsHighest()
    {
        using var database = new TestDatabase("""
            {
              "categories": [ { "code": "KAT_000007", "name": "Tools" }, { "code": "KAT_000002", "name": "Office" } ],
              "products": [
                { "code": "PROD_000009", "name": "Hammer", "categories": ["KAT_000007"] },
                { "code": "PROD_000004", "name": "Paper", "categories": ["KAT_000002"] }
              ],
              "clients": [ { "name": "Alpha", "categories": ["KAT_000007"], "stock": { "PROD_000009": 3 } } ]
            }
            """);
        using var host = database.StartHost();
        var http = host.CreateClient();

        // read the seeded data
        var categories = await http.GetFromJsonAsync<List<CategoryDto>>("api/categories");
        var hammer = await http.GetFromJsonAsync<RecordDetailDto>("api/products/PROD_000009");
        var rows = await http.GetFromJsonAsync<List<StockRowDto>>("api/stock");

        Assert.Equal("Tools", categories!.Single(category => category.Code == "KAT_000007").Name);
        Assert.Equal(["KAT_000007"], hammer!.CategoryCodes);
        Assert.Equal(3, rows!.Single().Quantity);

        // add a new category and a new product
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = new Category { Name = "New category" };
        var product = new Product { Name = "New product" };
        db.AddRange(category, product);
        await db.SaveChangesAsync();

        Assert.Equal("KAT_000008", category.Code);
        Assert.Equal("PROD_000010", product.Code);
    }

    /// <summary>
    /// A seed file with a category or product code in the wrong form, with a reference to an unknown category code, or with stock of a product outside the categories of the client,
    /// stops the startup with an exception that names the wrong value.
    /// </summary>
    [Theory]
    [InlineData("""
        { "categories": [ { "code": "NARADI", "name": "Tools" } ], "products": [], "clients": [] }
        """, "NARADI")]
    [InlineData("""
        { "categories": [ { "code": "KAT_1", "name": "Tools" } ], "products": [], "clients": [] }
        """, "KAT_1")]
    [InlineData("""
        { "categories": [ { "code": "PROD_000001", "name": "Tools" } ], "products": [], "clients": [] }
        """, "PROD_000001")]
    [InlineData("""
        { "categories": [ { "code": "KAT_000000", "name": "Tools" } ], "products": [], "clients": [] }
        """, "KAT_000000")]
    [InlineData("""
        { "categories": [], "products": [ { "code": "MOUSE", "name": "Mouse", "categories": [] } ], "clients": [] }
        """, "MOUSE")]
    [InlineData("""
        { "categories": [], "products": [ { "code": "KAT_000001", "name": "Mouse", "categories": [] } ], "clients": [] }
        """, "KAT_000001")]
    [InlineData("""
        { "categories": [], "products": [ { "code": "PROD_000001", "name": "Mouse", "categories": ["KAT_000099"] } ], "clients": [] }
        """, "KAT_000099")]
    [InlineData("""
        {
          "categories": [ { "code": "KAT_000001", "name": "Office" } ],
          "products": [ { "code": "PROD_000001", "name": "Paper", "categories": ["KAT_000001"] }, { "code": "PROD_000002", "name": "Other", "categories": [] } ],
          "clients": [ { "name": "Alpha", "categories": ["KAT_000001"], "stock": { "PROD_000002": 1 } } ]
        }
        """, "PROD_000002")]
    public void InvalidSeed_StopsStartup(string seed, string expectedInMessage)
    {
        using var database = new TestDatabase(seed);
        using var host = database.StartHost();

        var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());

        Assert.Contains(expectedInMessage, exception.Message);
    }
}
