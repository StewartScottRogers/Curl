using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/gziptrunc</c>: a gzip member cut short of its 8-byte CRC and size trailer; the connection is kept.</summary>
public sealed class GzipTruncRoute() : EncodingRoute("/gziptrunc", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "gzip")], ServerPyCompression.Gzip(Repeated("good data here\n", 20))[..^8]);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new GzipTruncRoute());
}
