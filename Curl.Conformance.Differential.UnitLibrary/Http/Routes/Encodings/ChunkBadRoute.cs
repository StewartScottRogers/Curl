using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/chunkbad</c>: a chunked body whose size line <c>zz</c> is not hexadecimal; the connection closes.</summary>
public sealed class ChunkBadRoute() : EncodingRoute("/chunkbad", closesConnection: true)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        Ascii("HTTP/1.1 200 OK\r\n" + DifferentialHttpResponseWriter.DateHeaderLine + "Transfer-Encoding: chunked\r\n\r\nzz\r\nhello\r\n");

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ChunkBadRoute());
}
