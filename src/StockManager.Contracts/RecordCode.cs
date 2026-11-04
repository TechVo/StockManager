namespace StockManager.Contracts;

/// <summary>Type of a record that has a code and a detail page.</summary>
public enum RecordType
{
    Client,
    Category,
    Product,
}

/// <summary>
/// Rules of record codes, shared by the server and the client. The server uses the prefixes to generate codes,
/// and the client uses them to find the type of a record from a code entered by the user.
/// </summary>
public static class RecordCode
{
    public const string ClientPrefix = "KLI_";

    public const string CategoryPrefix = "KAT_";

    public const string ProductPrefix = "PROD_";

    /// <summary>Converts text entered by the user to the form in which codes are stored.</summary>
    public static string Normalize(string text) => text.Trim().ToUpperInvariant();

    /// <summary>Returns the record type with the prefix of this code, or null if no type has this prefix.</summary>
    public static RecordType? GetRecordType(string code)
    {
        if (code.StartsWith(ClientPrefix, StringComparison.Ordinal))
        {
            return RecordType.Client;
        }

        if (code.StartsWith(CategoryPrefix, StringComparison.Ordinal))
        {
            return RecordType.Category;
        }

        if (code.StartsWith(ProductPrefix, StringComparison.Ordinal))
        {
            return RecordType.Product;
        }

        return null;
    }
}
