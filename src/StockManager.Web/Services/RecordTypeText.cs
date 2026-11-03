using StockManager.Contracts;

namespace StockManager.Web.Services;

/// <summary>Texts for record types shown to the user.</summary>
public static class RecordTypeText
{
    /// <summary>Returns the name of the record type.</summary>
    public static string GetName(RecordType type) => type switch
    {
        RecordType.Client => "Klient",
        RecordType.Category => "Kategorie",
        _ => "Produkt",
    };
}
