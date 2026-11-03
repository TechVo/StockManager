using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockManager.Contracts;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Api.Tests;

/// <summary>
/// Integration tests of the REST API against the real application host and a SQLite file.
/// Cover the filter options, category and product codes, which rows and categories are visible, filter combinations and quantity changes.
/// </summary>
public sealed class StockEndpointsTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly WebApplicationFactory<Program> _host;
    private readonly HttpClient _http;

    /// <summary>Starts the application with its own database for each test.</summary>
    public StockEndpointsTests()
    {
        _host = _database.StartHost();
        _http = _host.CreateClient();
    }

    /// <summary>
    /// The root address serves the page of the Blazor client, and an unknown API address returns 404 and not this page.
    /// </summary>
    [Fact]
    public async Task RootAddress_ServesPageAndUnknownApiReturns404()
    {
        // read the page and an address that does not exist
        var page = await _http.GetStringAsync("/");
        var unknown = await _http.GetAsync("api/unknown");

        Assert.Contains("Správa skladu", page);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>
    /// The filter option endpoints return all clients, categories and products from the seed.
    /// </summary>
    [Theory]
    [InlineData("api/clients", "Alpha", "Beta", "Gamma")]
    [InlineData("api/categories", "Electronics", "Garden", "Office")]
    [InlineData("api/products", "Hose", "Mouse", "Paper")]
    public async Task LookupEndpoints_ReturnAllRecords(string uri, params string[] expected)
    {
        var records = await _http.GetFromJsonAsync<List<LookupDto>>(uri);

        Assert.Equal(expected, records!.Select(record => record.Name).Order());
    }

    /// <summary>
    /// The category endpoint returns the code from the seed for each category.
    /// </summary>
    [Fact]
    public async Task Categories_ReturnCodeAndName()
    {
        var categories = await _http.GetFromJsonAsync<List<CategoryDto>>("api/categories");

        Assert.Equal(
            [("KAT_000001", "Electronics"), ("KAT_000002", "Office"), ("KAT_000003", "Garden")],
            categories!.Select(category => (category.Code, category.Name)).Order());
    }

    /// <summary>
    /// After a category gets a new name, it keeps its code and its products, and the grid shows the new name.
    /// </summary>
    [Fact]
    public async Task CategoryRename_KeepsCodeAndProducts()
    {
        // the API has no rename operation, so change the name directly in the database
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.SingleAsync(category => category.Code == "KAT_000001");
            category.Name = "Gadgets";
            await db.SaveChangesAsync();
        }

        // read the category and the rows again
        var categories = await _http.GetFromJsonAsync<List<CategoryDto>>("api/categories");
        var rows = await GetStockAsync("");

        Assert.Equal("Gadgets", categories!.Single(category => category.Code == "KAT_000001").Name);
        Assert.Equal(["Gadgets"], rows.Single(row => row is { ClientName: "Alpha", ProductName: "Mouse" }).Categories);
    }

    /// <summary>
    /// The product endpoint returns the code from the seed for each product.
    /// </summary>
    [Fact]
    public async Task Products_ReturnCodeAndName()
    {
        var products = await _http.GetFromJsonAsync<List<ProductDto>>("api/products");

        Assert.Equal(
            [("PROD_000001", "Mouse"), ("PROD_000002", "Paper"), ("PROD_000003", "Hose")],
            products!.Select(product => (product.Code, product.Name)).Order());
    }

    /// <summary>
    /// After a product gets a new name, it keeps its code and its stock, and the grid shows the new name.
    /// </summary>
    [Fact]
    public async Task ProductRename_KeepsCodeAndStock()
    {
        // the API has no rename operation, so change the name directly in the database
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.SingleAsync(product => product.Code == "PROD_000001");
            product.Name = "Trackball";
            await db.SaveChangesAsync();
        }

        // read the product and the rows again
        var products = await _http.GetFromJsonAsync<List<ProductDto>>("api/products");
        var rows = await GetStockAsync("");

        Assert.Equal("Trackball", products!.Single(product => product.Code == "PROD_000001").Name);
        Assert.Equal(5, rows.Single(row => row is { ClientName: "Alpha", ProductName: "Trackball" }).Quantity);
    }

    /// <summary>
    /// A new record gets the next code from the database, and the application can read it right after the save.
    /// </summary>
    [Theory]
    [InlineData("client", "KLI_000004")]
    [InlineData("category", "KAT_000004")]
    [InlineData("product", "PROD_000004")]
    public async Task NewRecord_GetsNextCode(string type, string expectedCode)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // save the record without a code
        var entry = db.Add<object>(type switch
        {
            "client" => new Client { Name = "New name" },
            "category" => new Category { Name = "New name" },
            _ => new Product { Name = "New name" },
        });
        await db.SaveChangesAsync();

        Assert.Equal(expectedCode, entry.Property<string>("Code").CurrentValue);
    }

    /// <summary>
    /// The code of a deleted record is not used again for a new record.
    /// </summary>
    [Fact]
    public async Task DeletedCode_IsNotUsedAgain()
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // delete the product with the highest number from the seed
        db.Remove(await db.Products.SingleAsync(product => product.Code == "PROD_000003"));
        await db.SaveChangesAsync();

        // add a new product
        var product = new Product { Name = "New product" };
        db.Add(product);
        await db.SaveChangesAsync();

        Assert.Equal("PROD_000004", product.Code);
    }

    /// <summary>
    /// The database rejects a direct SQL change of a code in each table, because the column is generated.
    /// </summary>
    [Theory]
    [InlineData("UPDATE Clients SET Code = 'KLI_000099' WHERE Id = 1")]
    [InlineData("UPDATE Categories SET Code = 'KAT_000099' WHERE Id = 1")]
    [InlineData("UPDATE Products SET Code = 'PROD_000099' WHERE Id = 1")]
    public async Task Code_CannotBeChangedBySql(string sql)
    {
        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exception = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("generated column", exception.Message);
    }

    /// <summary>
    /// Without a filter, each client has rows only for products from its categories, each with its own quantity.
    /// A product missing from the seed stock has quantity 0.
    /// </summary>
    [Fact]
    public async Task NoFilter_ReturnsOnlyProductsFromClientCategories()
    {
        var rows = await GetStockAsync("");

        Assert.Equal(
            ["Alpha/Mouse/5", "Beta/Mouse/7", "Beta/Paper/0", "Gamma/Hose/9"],
            rows.Select(row => $"{row.ClientName}/{row.ProductName}/{row.Quantity}").Order());
    }

    /// <summary>
    /// A row shows only the categories of the product that the client works with.
    /// Alpha does not work with Office, so its Mouse row has only Electronics.
    /// </summary>
    [Fact]
    public async Task Row_ShowsOnlyCategoriesOfTheClient()
    {
        var rows = await GetStockAsync("");

        Assert.Equal(["Electronics"], rows.Single(row => row is { ClientName: "Alpha", ProductName: "Mouse" }).Categories);
        Assert.Equal(["Electronics", "Office"], rows.Single(row => row is { ClientName: "Beta", ProductName: "Mouse" }).Categories.Order());
    }

    /// <summary>
    /// More values in one filter list select rows that match any of them.
    /// </summary>
    [Fact]
    public async Task ValuesInOneFilter_AreCombinedWithOr()
    {
        var alpha = await FindIdAsync("api/clients", "Alpha");
        var gamma = await FindIdAsync("api/clients", "Gamma");

        // select two clients
        var rows = await GetStockAsync($"clientIds={alpha}&clientIds={gamma}");

        Assert.Equal(["Alpha", "Gamma"], rows.Select(row => row.ClientName).Order());
    }

    /// <summary>
    /// Different filter lists must all match, so client Beta and product Mouse give one row.
    /// </summary>
    [Fact]
    public async Task DifferentFilters_AreCombinedWithAnd()
    {
        var beta = await FindIdAsync("api/clients", "Beta");
        var mouse = await FindIdAsync("api/products", "Mouse");

        // select one client and one product
        var rows = await GetStockAsync($"clientIds={beta}&productIds={mouse}");

        var row = Assert.Single(rows);
        Assert.Equal(("Beta", "Mouse"), (row.ClientName, row.ProductName));
    }

    /// <summary>
    /// The Office filter returns only rows of clients that work with Office.
    /// Alpha has Mouse, which is in Office, but Alpha does not work with Office, so its row is not returned.
    /// The row of Beta keeps all its categories, because the filter does not change their list.
    /// </summary>
    [Fact]
    public async Task CategoryFilter_IgnoresCategoriesTheClientDoesNotWorkWith()
    {
        var office = await FindIdAsync("api/categories", "Office");

        // select one category
        var rows = await GetStockAsync($"categoryIds={office}");

        Assert.Equal(["Beta/Mouse", "Beta/Paper"], rows.Select(row => $"{row.ClientName}/{row.ProductName}").Order());
        Assert.Equal(["Electronics", "Office"], rows.Single(row => row.ProductName == "Mouse").Categories.Order());
    }

    /// <summary>
    /// A valid change returns 204 and changes the quantity of this client only, not the same product at another client.
    /// </summary>
    [Fact]
    public async Task Put_ChangesQuantityOfOneClientOnly()
    {
        var alpha = await FindIdAsync("api/clients", "Alpha");
        var mouse = await FindIdAsync("api/products", "Mouse");

        // change the quantity at Alpha
        var response = await _http.PutAsJsonAsync($"api/stock/{alpha}/{mouse}", new UpdateStockRequest { Quantity = 42 });

        // Alpha has the new quantity, Beta keeps the quantity from the seed
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rows = await GetStockAsync($"productIds={mouse}");
        Assert.Equal(42, rows.Single(row => row.ClientName == "Alpha").Quantity);
        Assert.Equal(7, rows.Single(row => row.ClientName == "Beta").Quantity);
    }

    /// <summary>
    /// A negative quantity, a too high quantity, a missing quantity or a decimal number is rejected with 400 and the stock stays the same.
    /// </summary>
    [Theory]
    [InlineData("""{ "quantity": -1 }""")]
    [InlineData("""{ "quantity": 1000001 }""")]
    [InlineData("""{ }""")]
    [InlineData("""{ "quantity": 1.5 }""")]
    public async Task Put_InvalidBody_Returns400(string body)
    {
        var alpha = await FindIdAsync("api/clients", "Alpha");
        var mouse = await FindIdAsync("api/products", "Mouse");

        // send the invalid body
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"api/stock/{alpha}/{mouse}", content);

        // rejected, and the quantity from the seed stays
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var rows = await GetStockAsync($"clientIds={alpha}");
        Assert.Equal(5, Assert.Single(rows).Quantity);
    }

    /// <summary>
    /// A change of a product outside the categories of the client returns 404 and does not create a stock row.
    /// </summary>
    [Fact]
    public async Task Put_ProductOutsideClientCategories_Returns404()
    {
        var alpha = await FindIdAsync("api/clients", "Alpha");
        var hose = await FindIdAsync("api/products", "Hose");

        // try to change a product from a category that Alpha does not work with
        var response = await _http.PutAsJsonAsync($"api/stock/{alpha}/{hose}", new UpdateStockRequest { Quantity = 1 });

        // rejected, and no row is created
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(await GetStockAsync($"clientIds={alpha}"), row => row.ProductName == "Hose");
    }

    /// <summary>Stops the host and deletes the database of the test.</summary>
    public void Dispose()
    {
        _http.Dispose();
        _host.Dispose();
        _database.Dispose();
    }

    /// <summary>Reads the grid rows for the given query string.</summary>
    private async Task<List<StockRowDto>> GetStockAsync(string query) =>
        (await _http.GetFromJsonAsync<List<StockRowDto>>($"api/stock?{query}"))!;

    /// <summary>Finds the id of a filter option by its name.</summary>
    private async Task<int> FindIdAsync(string uri, string name)
    {
        var records = await _http.GetFromJsonAsync<List<LookupDto>>(uri);

        return records!.Single(record => record.Name == name).Id;
    }
}
