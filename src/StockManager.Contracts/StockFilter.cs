namespace StockManager.Contracts;

/// <summary>
/// Selection of grid rows, sent as query parameters of <c>GET /api/stock</c>.
/// A row must match one of the selected values in every list. An empty list does not limit the rows.
/// </summary>
public sealed record StockFilter(int[] ClientIds, int[] ProductIds, int[] CategoryIds)
{
    /// <summary>Returns a filter that selects all rows.</summary>
    /// <remarks>A method and not a static property, because the Minimal API validation walks static properties too and never ends.</remarks>
    public static StockFilter CreateEmpty() => new([], [], []);

    /// <summary>Tells whether the filter limits the rows.</summary>
    public bool HasSelection() => ClientIds.Length > 0 || ProductIds.Length > 0 || CategoryIds.Length > 0;
}
