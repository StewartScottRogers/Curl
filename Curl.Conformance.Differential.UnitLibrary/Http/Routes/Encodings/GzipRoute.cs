using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/gzip</c>: fifty lines of <c>gzipped body</c> gzipped as text; the connection is kept.</summary>
public sealed class GzipRoute() : EncodingRoute("/gzip", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "gzip"), ("Content-Type", "text/plain")], ServerPyCompression.Gzip(Repeated("gzipped body\n", 50)));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new GzipRoute());
}
