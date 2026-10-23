using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/deflate</c>: fifty lines of <c>deflated body</c> as a zlib stream under <c>Content-Encoding: deflate</c>; the connection is kept.</summary>
public sealed class DeflateRoute() : EncodingRoute("/deflate", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "deflate")], ServerPyCompression.Zlib(Repeated("deflated body\n", 50)));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new DeflateRoute());
}
