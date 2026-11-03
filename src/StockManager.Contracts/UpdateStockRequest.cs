using System.ComponentModel.DataAnnotations;

namespace StockManager.Contracts;

/// <summary>
/// JSON body of <c>PUT /api/stock/{clientId}/{productId}</c>. Defines the allowed quantity range,
/// which the built-in Minimal API validation enforces before the handler runs.
/// </summary>
public sealed record UpdateStockRequest
{
    /// <summary>Highest quantity that can be stored for one product at one client.</summary>
    public const int MaxQuantity = 1_000_000;

    // required, so that a body without a quantity is rejected and does not reset the stock to 0
    [Range(0, MaxQuantity)]
    public required int Quantity { get; init; }
}
