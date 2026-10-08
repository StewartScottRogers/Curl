namespace Curl.Protocol.Http;

/// <summary>
/// Pins the text of <see cref="HttpConnectionInfoLines" />' formatted lines against curl 8.21.0.
/// </summary>
[TestClass]
public sealed class HttpConnectionInfoLinesTests
{
    /// <summary>
    /// Measured (BL-1527 Notes): a 301 under <c>-L</c> arriving while a 2097152-byte body is sent
    /// writes <c>close instead of sending 1441947 more bytes</c>, and with
    /// <c>Transfer-Encoding: chunked</c> <c>close instead of sending unknown amount of more bytes</c>.
    /// </summary>
    /// <param name="knownLength">The body's length, or -1 for a body of unknown length.</param>
    /// <param name="expected">The line curl writes.</param>
    [TestMethod]
    [DataRow(2097152L, "close instead of sending 1441947 more bytes", DisplayName = "known length")]
    [DataRow(-1L, "close instead of sending unknown amount of more bytes", DisplayName = "unknown length")]
    public void CloseInsteadOfSending_WritesCurlsLine(long knownLength, string expected)
    {
        long? length = knownLength < 0 ? null : knownLength;

        string line = HttpConnectionInfoLines.CloseInsteadOfSending(length, 655205);

        Assert.AreEqual(expected, line);
    }
}
