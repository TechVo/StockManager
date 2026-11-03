using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockManager.Contracts;

namespace StockManager.Web.Services;

/// <summary>
/// Client of the REST API. All communication between the pages and the server goes through this class,
/// which also converts failed changes to messages for the user.
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

    /// <summary>Returns all categories with their codes for the detail page.</summary>
    public Task<List<CategoryDto>> GetCategoryOptionsAsync() => GetAsync<List<CategoryDto>>("api/categories");

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

    /// <summary>Saves the values of a record. Returns a message for the user if it failed, or null if it was saved.</summary>
    public async Task<string?> UpdateRecordAsync(RecordType type, string code, string name, IEnumerable<string> categoryCodes)
    {
        try
        {
            // a category has only a name, the other types also have categories
            var uri = GetRecordUri(type, code);
            var response = type == RecordType.Category
                ? await _http.PutAsJsonAsync(uri, new UpdateCategoryRequest { Name = name })
                : await _http.PutAsJsonAsync(uri, new UpdateCategorizedRecordRequest { Name = name, CategoryCodes = categoryCodes.ToList() });

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            // a record that does not exist has no message from the server
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return "Záznam už neexistuje.";
            }

            // validation errors have no detail text, so they use a general message
            var problem = await ReadProblemAsync(response);
            return problem?.Detail ?? (response.StatusCode == HttpStatusCode.BadRequest ? "Zkontrolujte zadané hodnoty." : SaveFailedMessage);
        }
        catch (HttpRequestException)
        {
            // the server cannot be reached
            return SaveFailedMessage;
        }
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

    /// <summary>Reads the problem details of a failed response, or returns null if the body has another format.</summary>
    private static async Task<ProblemResponse?> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemResponse>();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Reads a JSON response, which must not be empty.</summary>
    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken = default) =>
        await _http.GetFromJsonAsync<T>(uri, cancellationToken)
            ?? throw new HttpRequestException($"Empty response from '{uri}'.");

    /// <summary>The part of a problem details response that the client uses.</summary>
    private sealed record ProblemResponse(string? Detail);
}
