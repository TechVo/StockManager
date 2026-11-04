using System.ComponentModel.DataAnnotations;
using System.Globalization;

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

    /// <summary>Reads a quantity entered by the user. Only digits are allowed, so a sign, spaces, a decimal point or an exponent like 1e5 are rejected.</summary>
    public static bool TryParseQuantity(string text, out int quantity) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out quantity);
}
