namespace Curl.Http2;

/// <summary>
/// Shared helpers for the HPACK tests: hex with spaces, and header lists written briefly.
/// </summary>
internal static class Hpack
{
    /// <summary>
    /// Parses hex written as RFC 7541 appendix C prints it, in groups separated by spaces.
    /// </summary>
    public static byte[] FromHex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>
    /// Builds a header list from name and value pairs.
    /// </summary>
    public static HeaderField[] Fields(params (string Name, string Value)[] fields) =>
        [.. fields.Select(field => new HeaderField(field.Name, field.Value))];

    /// <summary>
    /// Asserts a decoded header list equals the expected one, field by field.
    /// </summary>
    public static void AssertFields(IReadOnlyList<HeaderField> actual, params HeaderField[] expected) =>
        CollectionAssert.AreEqual(expected, actual.ToArray());

    /// <summary>
    /// Runs <paramref name="action" /> and returns the <see cref="HpackDecodingError" /> it failed with.
    /// </summary>
    public static HpackDecodingError ErrorOf(Action action) =>
        Assert.ThrowsExactly<HpackDecodingException>(action).Error;
}
