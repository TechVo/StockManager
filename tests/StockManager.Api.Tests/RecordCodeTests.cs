using StockManager.Contracts;

namespace StockManager.Api.Tests;

/// <summary>
/// Tests of the rules of record codes, which the code search uses to find the record type from the entered text.
/// </summary>
public sealed class RecordCodeTests
{
    /// <summary>
    /// A code with the prefix of a type gives this type, also when only the prefix is entered.
    /// </summary>
    [Theory]
    [InlineData("KLI_000001", RecordType.Client)]
    [InlineData("KAT_000001", RecordType.Category)]
    [InlineData("PROD_000001", RecordType.Product)]
    [InlineData("PROD_", RecordType.Product)]
    public void KnownPrefix_GivesRecordType(string code, RecordType expected)
    {
        Assert.Equal(expected, RecordCode.GetRecordType(code));
    }

    /// <summary>
    /// Text without the exact prefix of a type gives no type: an unknown prefix, no underscore, lowercase letters or empty text.
    /// </summary>
    [Theory]
    [InlineData("XYZ_000001")]
    [InlineData("KAT")]
    [InlineData("kat_000001")]
    [InlineData("A_KAT_000001")]
    [InlineData("")]
    public void UnknownPrefix_GivesNoRecordType(string code)
    {
        Assert.Null(RecordCode.GetRecordType(code));
    }
}
