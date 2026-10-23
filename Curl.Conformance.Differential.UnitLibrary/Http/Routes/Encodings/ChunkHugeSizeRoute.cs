using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/chunk-huge-size</c>: a chunk size of seventeen hexadecimal digits, past 64 bits; the connection closes.</summary>
public sealed class ChunkHugeSizeRoute() : EncodingRoute("/chunk-huge-size", closesConnection: true)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        Ascii("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFFF\r\nx\r\n");

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ChunkHugeSizeRoute());
}
