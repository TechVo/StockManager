using StockManager.Contracts;
using StockManager.Server.Services;

namespace StockManager.Server.Endpoints;

/// <summary>
/// Defines the REST API of the detail pages: reading and changing one client, category or product, found by its code.
/// </summary>
public static class RecordEndpoints
{
    /// <summary>Maps all endpoints under <c>/api</c>.</summary>
    public static void MapRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // all endpoints share one address prefix
        var api = endpoints.MapGroup("/api");

        // clients
        api.MapGet("/clients/{code}", async (string code, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.GetClientAsync(code, cancellationToken)));
        api.MapPut("/clients/{code}", async (string code, UpdateCategorizedRecordRequest request, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.UpdateClientAsync(code, request, cancellationToken)));

        // categories
        api.MapGet("/categories/{code}", async (string code, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.GetCategoryAsync(code, cancellationToken)));
        api.MapPut("/categories/{code}", async (string code, UpdateCategoryRequest request, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.UpdateCategoryAsync(code, request, cancellationToken)));

        // products
        api.MapGet("/products/{code}", async (string code, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.GetProductAsync(code, cancellationToken)));
        api.MapPut("/products/{code}", async (string code, UpdateCategorizedRecordRequest request, RecordService service, CancellationToken cancellationToken) =>
            ToResult(await service.UpdateProductAsync(code, request, cancellationToken)));
    }

    /// <summary>Returns the detail, or 404 if it does not exist.</summary>
    private static IResult ToResult(RecordDetailDto? detail) =>
        detail is null ? Results.NotFound() : Results.Ok(detail);

    /// <summary>Converts the result of a change to the HTTP response, with the message for the user in the problem details.</summary>
    private static IResult ToResult(RecordUpdateResult result) => result.Status switch
    {
        RecordUpdateStatus.Updated => Results.NoContent(),
        RecordUpdateStatus.NotFound => Results.NotFound(),
        RecordUpdateStatus.Invalid => Results.Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
    };
}
