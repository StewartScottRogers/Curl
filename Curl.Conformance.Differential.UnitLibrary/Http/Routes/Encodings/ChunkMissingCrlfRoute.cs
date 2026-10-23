using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/chunk-missing-crlf</c>: a chunk followed by <c>XX</c> where its CRLF belongs; the connection closes.</summary>
public sealed class ChunkMissingCrlfRoute() : EncodingRoute("/chunk-missing-crlf", closesConnection: true)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        Ascii("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabcXX0\r\n\r\n");

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ChunkMissingCrlfRoute());
}
