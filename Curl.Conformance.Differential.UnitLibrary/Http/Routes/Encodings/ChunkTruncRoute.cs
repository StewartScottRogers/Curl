using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/chunk-trunc</c>: a 5-byte chunk cut off after 2 bytes; the connection closes.</summary>
public sealed class ChunkTruncRoute() : EncodingRoute("/chunk-trunc", closesConnection: true)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        Ascii("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nab");

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ChunkTruncRoute());
}
