using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// Shared helpers for the QPACK tests: hex with spaces, header lists written briefly, and
/// the connection error a failure reports.
/// </summary>
internal static class Qpack
{
    /// <summary>
    /// Parses hex written as RFC 9204 appendix B prints it, in groups separated by spaces.
    /// </summary>
    public static byte[] FromHex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>
    /// Builds a header list from name and value pairs.
    /// </summary>
    public static HeaderField[] Fields(params (string Name, string Value)[] fields) =>
        [.. fields.Select(field => new HeaderField(field.Name, field.Value))];

    /// <summary>
    /// Decodes a section the test expects to decode, and returns its field lines.
    /// </summary>
    public static HeaderField[] Decode(QpackDecoder decoder, long streamId, byte[] section)
    {
        Assert.IsTrue(decoder.TryDecodeFieldSection(streamId, section, out var fields));
        return [.. fields];
    }

    /// <summary>
    /// Runs <paramref name="action" /> and returns the <see cref="QpackErrorCode" /> it failed with.
    /// </summary>
    public static QpackErrorCode ErrorOf(Action action) =>
        Assert.ThrowsExactly<QpackException>(action).ErrorCode;
}
