using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/gziptrail</c>: a whole gzip member followed by <c>TRAILING GARBAGE</c>; the connection is kept.</summary>
public sealed class GzipTrailRoute() : EncodingRoute("/gziptrail", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "gzip")], [.. ServerPyCompression.Gzip(Ascii("good\n")), .. Ascii("TRAILING GARBAGE")]);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new GzipTrailRoute());
}
