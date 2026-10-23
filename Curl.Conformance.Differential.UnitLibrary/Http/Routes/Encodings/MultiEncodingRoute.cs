using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/multienc</c>: <c>layered</c> zlib-compressed then gzipped under <c>Content-Encoding: deflate, gzip</c>; the connection is kept.</summary>
public sealed class MultiEncodingRoute() : EncodingRoute("/multienc", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "deflate, gzip")], ServerPyCompression.Gzip(ServerPyCompression.Zlib(Ascii("layered\n"))));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new MultiEncodingRoute());
}
