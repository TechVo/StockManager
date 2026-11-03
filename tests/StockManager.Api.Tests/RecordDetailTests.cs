using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockManager.Contracts;

namespace StockManager.Api.Tests;

/// <summary>
/// Integration tests of the detail API against the real application host and a SQLite file.
/// Cover reading and changing records by code, the rules for names, and how a change of categories changes stock rows.
/// </summary>
public sealed class RecordDetailTests : IDisposable
{
    private const string Alpha = "api/clients/KLI_000001";
    private const string Beta = "api/clients/KLI_000002";
    private const string Gamma = "api/clients/KLI_000003";
    private const string Mouse = "api/products/PROD_000001";
    private const string Paper = "api/products/PROD_000002";
    private const string Electronics = "KAT_000001";
    private const string Office = "KAT_000002";
    private const string Garden = "KAT_000003";

    private readonly TestDatabase _database = new();
    private readonly WebApplicationFactory<Program> _host;
    private readonly HttpClient _http;

    /// <summary>Starts the application with its own database for each test.</summary>
    public RecordDetailTests()
    {
        _host = _database.StartHost();
        _http = _host.CreateClient();
    }

    /// <summary>
    /// The address of a detail page serves the page of the Blazor client, so that a detail can be opened directly.
    /// </summary>
    [Fact]
    public async Task DetailAddress_ServesPage()
    {
        var page = await _http.GetStringAsync("detail/KAT_000001");

        Assert.Contains("Správa skladu", page);
    }

    /// <summary>
    /// The detail of each record type returns its code, name and categories. A category has no categories.
    /// </summary>
    [Theory]
    [InlineData(Beta, "KLI_000002", "Beta", new[] { Electronics, Office })]
    [InlineData(Mouse, "PROD_000001", "Mouse", new[] { Electronics, Office })]
    [InlineData("api/categories/KAT_000002", "KAT_000002", "Office", new string[0])]
    public async Task Detail_ReturnsCodeNameAndCategories(string uri, string code, string name, string[] categoryCodes)
    {
        var detail = await _http.GetFromJsonAsync<RecordDetailDto>(uri);

        Assert.Equal(code, detail!.Code);
        Assert.Equal(name, detail.Name);
        Assert.Equal(categoryCodes, detail.CategoryCodes.Order());
    }

    /// <summary>
    /// A code that does not exist returns 404 for reading and for changing, also when the prefix is of another type.
    /// </summary>
    [Theory]
    [InlineData("api/clients/KLI_999999")]
    [InlineData("api/products/KAT_000001")]
    public async Task UnknownCode_Returns404(string uri)
    {
        var read = await _http.GetAsync(uri);
        var change = await _http.PutAsJsonAsync(uri, CreateRequest("New", Office));

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
    }

    /// <summary>
    /// A new category name is saved without spaces around it and is shown in the grid, and the code stays the same.
    /// </summary>
    [Fact]
    public async Task CategoryName_IsChanged()
    {
        var response = await _http.PutAsJsonAsync("api/categories/KAT_000001", new UpdateCategoryRequest { Name = "  Gadgets " });
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["Gadgets"], rows.Single(row => row is { ClientName: "Alpha", ProductName: "Mouse" }).Categories);
    }

    /// <summary>
    /// A new name of a client and of a product is shown in the grid, and the quantity of their row stays the same.
    /// </summary>
    [Fact]
    public async Task ClientAndProductName_AreChanged()
    {
        await _http.PutAsJsonAsync(Alpha, CreateRequest("Alpha 2", Electronics));
        await _http.PutAsJsonAsync(Mouse, CreateRequest("Mouse 2", Electronics, Office));

        var rows = await GetRowsAsync();

        Assert.Equal(5, rows.Single(row => row is { ClientName: "Alpha 2", ProductName: "Mouse 2" }).Quantity);
    }

    /// <summary>
    /// A name that another record of the same type has returns 409 with a message, and a record can keep its own name.
    /// </summary>
    [Fact]
    public async Task DuplicateName_Returns409()
    {
        var duplicate = await _http.PutAsJsonAsync("api/categories/KAT_000001", new UpdateCategoryRequest { Name = "Office" });
        var ownName = await _http.PutAsJsonAsync("api/categories/KAT_000001", new UpdateCategoryRequest { Name = "Electronics" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("název", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, ownName.StatusCode);
    }

    /// <summary>
    /// An empty name, a name of only spaces and a name that is too long return 400.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InvalidName_Returns400(string? name)
    {
        // a null value means a name over the limit
        name ??= new string('x', UpdateCategoryRequest.MaxNameLength + 1);

        var response = await _http.PutAsJsonAsync(Paper, CreateRequest(name, Office));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A category code that does not exist returns 400 and changes nothing.
    /// </summary>
    [Fact]
    public async Task UnknownCategoryCode_Returns400()
    {
        var response = await _http.PutAsJsonAsync(Paper, CreateRequest("New", "KAT_999999"));
        var detail = await _http.GetFromJsonAsync<RecordDetailDto>(Paper);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Paper", detail!.Name);
    }

    /// <summary>
    /// A new category of a product creates a stock row with quantity 0 for the clients of this category.
    /// </summary>
    [Fact]
    public async Task ProductCategoryAdded_CreatesEmptyStockRow()
    {
        var response = await _http.PutAsJsonAsync(Paper, CreateRequest("Paper", Office, Garden));
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, rows.Single(row => row is { ClientName: "Gamma", ProductName: "Paper" }).Quantity);
    }

    /// <summary>
    /// A removed category of a product deletes its empty stock rows, because no client can see them any more.
    /// </summary>
    [Fact]
    public async Task ProductCategoryRemoved_DeletesEmptyStockRow()
    {
        var response = await _http.PutAsJsonAsync(Paper, CreateRequest("Paper"));
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(rows, row => row.ProductName == "Paper");
    }

    /// <summary>
    /// A removed category of a product keeps the rows of clients that still share another category with the product.
    /// </summary>
    [Fact]
    public async Task ProductCategoryRemoved_KeepsRowsStillVisible()
    {
        var response = await _http.PutAsJsonAsync(Mouse, CreateRequest("Mouse", Electronics));
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(7, rows.Single(row => row is { ClientName: "Beta", ProductName: "Mouse" }).Quantity);
    }

    /// <summary>
    /// A change that would hide a stock row with a quantity returns 409 with the client in the message, and changes nothing, not even the name.
    /// </summary>
    [Fact]
    public async Task ProductCategoryRemovedWithQuantity_Returns409AndChangesNothing()
    {
        var response = await _http.PutAsJsonAsync(Mouse, CreateRequest("Renamed", Office));
        var detail = await _http.GetFromJsonAsync<RecordDetailDto>(Mouse);
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Alpha (5 ks)", await response.Content.ReadAsStringAsync());
        Assert.Equal("Mouse", detail!.Name);
        Assert.Equal([Electronics, Office], detail.CategoryCodes.Order());
        Assert.Equal(5, rows.Single(row => row is { ClientName: "Alpha", ProductName: "Mouse" }).Quantity);
    }

    /// <summary>
    /// A new category of a client creates stock rows with quantity 0 for the products of this category, and keeps existing rows.
    /// </summary>
    [Fact]
    public async Task ClientCategoryAdded_CreatesEmptyStockRow()
    {
        var response = await _http.PutAsJsonAsync(Alpha, CreateRequest("Alpha", Electronics, Office));
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, rows.Single(row => row is { ClientName: "Alpha", ProductName: "Paper" }).Quantity);
        Assert.Equal(5, rows.Single(row => row is { ClientName: "Alpha", ProductName: "Mouse" }).Quantity);
    }

    /// <summary>
    /// A removed category of a client deletes the empty rows of products that the client can no longer see.
    /// </summary>
    [Fact]
    public async Task ClientCategoryRemoved_DeletesEmptyStockRow()
    {
        var response = await _http.PutAsJsonAsync(Beta, CreateRequest("Beta", Electronics));
        var rows = await GetRowsAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(rows, row => row is { ClientName: "Beta", ProductName: "Paper" });
        Assert.Equal(7, rows.Single(row => row is { ClientName: "Beta", ProductName: "Mouse" }).Quantity);
    }

    /// <summary>
    /// A client cannot lose a category while it has a quantity of a product from it: the response is 409 with the product in the message.
    /// </summary>
    [Fact]
    public async Task ClientCategoryRemovedWithQuantity_Returns409()
    {
        var response = await _http.PutAsJsonAsync(Gamma, CreateRequest("Gamma"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Hose (9 ks)", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Requests that change the same name at the same time do not fail with a server error: one is saved and the others return 409.
    /// </summary>
    [Fact]
    public async Task ConcurrentChanges_SaveOneAndRejectOthers()
    {
        // each category tries to take the same new name
        var requests = new[] { "KAT_000001", "KAT_000002", "KAT_000003" }
            .Select(code => _http.PutAsJsonAsync($"api/categories/{code}", new UpdateCategoryRequest { Name = "Same name" }));

        var responses = await Task.WhenAll(requests);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    /// <summary>Creates the body of a change of a client or a product.</summary>
    private static UpdateCategorizedRecordRequest CreateRequest(string name, params string[] categoryCodes) =>
        new() { Name = name, CategoryCodes = categoryCodes };

    /// <summary>Returns all grid rows.</summary>
    private async Task<List<StockRowDto>> GetRowsAsync() =>
        (await _http.GetFromJsonAsync<List<StockRowDto>>("api/stock"))!;

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _host.Dispose();
        _database.Dispose();
    }
}
