using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/chunked</c>: two chunks, the second with an extension, then an <c>X-T</c> trailer; the connection is kept.</summary>
public sealed class ChunkedRoute() : EncodingRoute("/chunked", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        Ascii("HTTP/1.1 200 OK\r\n" + DifferentialHttpResponseWriter.DateHeaderLine + "Transfer-Encoding: chunked\r\nTrailer: X-T\r\n\r\n5\r\nhello\r\n6;ext=1\r\n world\r\n0\r\nX-T: trailer\r\n\r\n");

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ChunkedRoute());
}
