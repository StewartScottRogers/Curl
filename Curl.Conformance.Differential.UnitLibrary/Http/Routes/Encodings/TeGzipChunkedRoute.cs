using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/te-gzip-chunked</c>: a gzip member as one chunk under <c>Transfer-Encoding: gzip, chunked</c>, with no <c>Date</c>; the connection is kept.</summary>
public sealed class TeGzipChunkedRoute() : EncodingRoute("/te-gzip-chunked", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        ChunkedGzip(ServerPyCompression.Gzip(Ascii("te gz\n")));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new TeGzipChunkedRoute());
}
