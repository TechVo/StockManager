using System.Net;
using System.Net.Http.Json;
using StockManager.Contracts;

namespace StockManager.Web.Services;

/// <summary>
/// Client of the REST API. All communication between the pages and the server goes through this class,
/// which also converts failed quantity updates to messages for the user.
/// </summary>
public sealed class StockApiClient
{
    private const string SaveFailedMessage = "Uložení se nezdařilo.";

    private readonly HttpClient _http;

    /// <summary>Message for a quantity that is not an allowed whole number.</summary>
    public static string InvalidQuantityMessage { get; } = $"Zadejte celé číslo od 0 do {UpdateStockRequest.MaxQuantity}.";

    /// <summary>Receives the HTTP client configured with the server address.</summary>
    public StockApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Returns the client options for the filter menu, which needs only the id and the name from the response.</summary>
    public Task<List<LookupDto>> GetClientsAsync() => GetAsync<List<LookupDto>>("api/clients");

    /// <summary>Returns the category options for the filter menu, which needs only the id and the name from the response.</summary>
    public Task<List<LookupDto>> GetCategoriesAsync() => GetAsync<List<LookupDto>>("api/categories");

    /// <summary>Returns the product options for the filter menu, which needs only the id and the name from the response.</summary>
    public Task<List<LookupDto>> GetProductsAsync() => GetAsync<List<LookupDto>>("api/products");

    /// <summary>Returns the grid rows that match the filter.</summary>
    public Task<List<StockRowDto>> GetStockAsync(StockFilter filter, CancellationToken cancellationToken)
    {
        // one query parameter for each selected value
        var parameters = filter.ClientIds.Select(id => $"clientIds={id}")
            .Concat(filter.ProductIds.Select(id => $"productIds={id}"))
            .Concat(filter.CategoryIds.Select(id => $"categoryIds={id}"));

        return GetAsync<List<StockRowDto>>($"api/stock?{string.Join('&', parameters)}", cancellationToken);
    }

    /// <summary>Saves a new quantity. Returns a message for the user if it failed, or null if it was saved.</summary>
    public async Task<string?> UpdateQuantityAsync(int clientId, int productId, int quantity)
    {
        try
        {
            // send the new quantity
            var request = new UpdateStockRequest { Quantity = quantity };
            var response = await _http.PutAsJsonAsync($"api/stock/{clientId}/{productId}", request);

            // convert the response to a message for the user
            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => null,
                HttpStatusCode.BadRequest => InvalidQuantityMessage,
                HttpStatusCode.NotFound => "Klient tento produkt nemá.",
                _ => SaveFailedMessage,
            };
        }
        catch (HttpRequestException)
        {
            // the server cannot be reached
            return SaveFailedMessage;
        }
    }

    /// <summary>Returns the detail of a record, or null if the code does not exist.</summary>
    public async Task<RecordDetailDto?> GetDetailAsync(RecordType type, string code)
    {
        // a missing record is a valid result and not a failure
        var response = await _http.GetAsync(GetRecordUri(type, code));
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RecordDetailDto>();
    }

    /// <summary>Returns the API address of a record of the given type.</summary>
    private static string GetRecordUri(RecordType type, string code)
    {
        var collection = type switch
        {
            RecordType.Client => "clients",
            RecordType.Category => "categories",
            _ => "products",
        };

        return $"api/{collection}/{Uri.EscapeDataString(code)}";
    }

    /// <summary>Reads a JSON response, which must not be empty.</summary>
    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken = default) =>
        await _http.GetFromJsonAsync<T>(uri, cancellationToken)
            ?? throw new HttpRequestException($"Empty response from '{uri}'.");
}
