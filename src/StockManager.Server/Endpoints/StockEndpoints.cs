using StockManager.Contracts;
using StockManager.Server.Services;

namespace StockManager.Server.Endpoints;

/// <summary>
/// Defines the REST API used by the Blazor client: lists of options for the filter menu, the details of records,
/// the filtered grid rows and the change of a stock quantity.
/// </summary>
public static class StockEndpoints
{
    /// <summary>Maps all endpoints under <c>/api</c>.</summary>
    public static void MapStockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // all endpoints share one address prefix
        var api = endpoints.MapGroup("/api");

        // options for the filter menu
        api.MapGet("/clients", (StockService service, CancellationToken cancellationToken) => service.GetClientsAsync(cancellationToken));
        api.MapGet("/categories", (StockService service, CancellationToken cancellationToken) => service.GetCategoriesAsync(cancellationToken));
        api.MapGet("/products", (StockService service, CancellationToken cancellationToken) => service.GetProductsAsync(cancellationToken));

        // details of records, found by code
        api.MapGet("/clients/{code}", async (string code, StockService service, CancellationToken cancellationToken) => ToResult(await service.GetClientDetailAsync(code, cancellationToken)));
        api.MapGet("/categories/{code}", async (string code, StockService service, CancellationToken cancellationToken) => ToResult(await service.GetCategoryDetailAsync(code, cancellationToken)));
        api.MapGet("/products/{code}", async (string code, StockService service, CancellationToken cancellationToken) => ToResult(await service.GetProductDetailAsync(code, cancellationToken)));

        // grid rows and the change of a quantity
        api.MapGet("/stock", ([AsParameters] StockFilter filter, StockService service, CancellationToken cancellationToken) => service.GetStockAsync(filter, cancellationToken));
        api.MapPut("/stock/{clientId:int}/{productId:int}", UpdateStockAsync);
    }

    /// <summary>Returns the detail, or 404 if it does not exist.</summary>
    private static IResult ToResult(RecordDetailDto? detail) =>
        detail is null ? Results.NotFound() : Results.Ok(detail);

    /// <summary>Changes a quantity. Returns 204, or 404 if the client does not have this product.</summary>
    private static async Task<IResult> UpdateStockAsync(
        int clientId,
        int productId,
        UpdateStockRequest request,
        StockService service,
        CancellationToken cancellationToken)
    {
        // no row was changed if the client does not have this product
        var updated = await service.UpdateQuantityAsync(clientId, productId, request.Quantity, cancellationToken);

        return updated ? Results.NoContent() : Results.NotFound();
    }
}
