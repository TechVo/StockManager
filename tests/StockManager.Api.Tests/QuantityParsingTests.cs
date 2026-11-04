using StockManager.Contracts;

namespace StockManager.Api.Tests;

/// <summary>
/// Tests of reading a quantity entered by the user, which allows only a number written with digits.
/// </summary>
public sealed class QuantityParsingTests
{
    /// <summary>
    /// Text with only digits gives the number, also with zeros at the start.
    /// </summary>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("007", 7)]
    public void Digits_GiveQuantity(string text, int expected)
    {
        var parsed = UpdateStockRequest.TryParseQuantity(text, out var quantity);

        Assert.True(parsed);
        Assert.Equal(expected, quantity);
    }

    /// <summary>
    /// Text with anything other than digits, or a number too large for the type, is rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("1e5")]
    [InlineData("12.5")]
    [InlineData("12,5")]
    [InlineData("1 000")]
    [InlineData("+5")]
    [InlineData("-5")]
    [InlineData(" 5")]
    [InlineData("5 ")]
    [InlineData("abc")]
    [InlineData("99999999999")]
    public void OtherText_IsRejected(string text)
    {
        Assert.False(UpdateStockRequest.TryParseQuantity(text, out _));
    }
}
