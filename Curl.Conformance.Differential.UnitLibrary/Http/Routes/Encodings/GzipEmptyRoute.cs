using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/gzipempty</c>: an empty body under <c>Content-Encoding: gzip</c>; the connection is kept.</summary>
public sealed class GzipEmptyRoute() : EncodingRoute("/gzipempty", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "gzip")], []);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new GzipEmptyRoute());
}
