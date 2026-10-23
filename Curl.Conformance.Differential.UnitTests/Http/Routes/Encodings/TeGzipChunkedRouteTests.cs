using System.Globalization;
using System.IO.Compression;
using System.Text;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/te-gzip-chunked</c>: the head, the chunk framing and the gzip member it carries.</summary>
[TestClass]
public sealed class TeGzipChunkedRouteTests
{
    private const string Head = "HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n";

    [TestMethod]
    public async Task TeGzipChunkedRoute_TwoRequests_AnswersOneGzipChunkAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new TeGzipChunkedRoute(), "/te-gzip-chunked");

        Assert.AreEqual(Head, head);
        var text = Text(body);
        var sizeEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        var size = int.Parse(text[..sizeEnd], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        Assert.AreEqual(size.ToString("x", CultureInfo.InvariantCulture), text[..sizeEnd]);
        var member = body[(sizeEnd + 2)..(sizeEnd + 2 + size)];
        Assert.AreEqual("\r\n0\r\n\r\n", Text(body[(sizeEnd + 2 + size)..]));
        CollectionAssert.AreEqual(PythonGzipHeader, member[..10]);
        Assert.AreEqual("te gz\n", Encoding.ASCII.GetString(Decompress(member, s => new GZipStream(s, CompressionMode.Decompress))));
    }
}
