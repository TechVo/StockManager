using StockManager.Contracts;

namespace StockManager.Web.Services;

/// <summary>Texts for record types shown to the user.</summary>
public static class RecordTypeText
{
    /// <summary>Returns the name of the record type.</summary>
    public static string GetName(RecordType type)
    {
        string name;

        switch (type)
        {
            case RecordType.Client:
                name = "Klient";
                break;
            case RecordType.Category:
                name = "Kategorie";
                break;
            case RecordType.Product:
                name = "Produkt";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        return name;
    }
}
