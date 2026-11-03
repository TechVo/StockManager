using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockManager.Contracts;

namespace StockManager.Api.Tests;

/// <summary>
/// Integration tests of the detail API against the real application host and a SQLite file.
/// Cover reading records by code, and that the API offers no way to change them.
/// </summary>
public sealed class RecordDetailTests : IDisposable
{
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
    /// The detail of each record type returns its id, code, name and category names. A category has no categories.
    /// </summary>
    [Theory]
    [InlineData("api/clients/KLI_000002", 2, "KLI_000002", "Beta", new[] { "Electronics", "Office" })]
    [InlineData("api/products/PROD_000001", 1, "PROD_000001", "Mouse", new[] { "Electronics", "Office" })]
    [InlineData("api/categories/KAT_000002", 2, "KAT_000002", "Office", new string[0])]
    public async Task Detail_ReturnsRecord(string uri, int id, string code, string name, string[] categories)
    {
        var detail = await _http.GetFromJsonAsync<RecordDetailDto>(uri);

        Assert.Equal(id, detail!.Id);
        Assert.Equal(code, detail.Code);
        Assert.Equal(name, detail.Name);
        Assert.Equal(categories, detail.Categories.Order());
    }

    /// <summary>
    /// A code that does not exist returns 404, also when the prefix is of another type.
    /// </summary>
    [Theory]
    [InlineData("api/clients/KLI_999999")]
    [InlineData("api/products/KAT_000001")]
    public async Task UnknownCode_Returns404(string uri)
    {
        var response = await _http.GetAsync(uri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The API has no way to change a record, because the quantity in stock is the only value that can be changed.
    /// </summary>
    [Theory]
    [InlineData("api/clients/KLI_000001")]
    [InlineData("api/categories/KAT_000001")]
    [InlineData("api/products/PROD_000001")]
    public async Task RecordChange_ReturnsMethodNotAllowed(string uri)
    {
        var response = await _http.PutAsJsonAsync(uri, new { name = "New name" });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _host.Dispose();
        _database.Dispose();
    }
}
