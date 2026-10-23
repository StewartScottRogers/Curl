using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/rawdeflate</c>: <c>raw deflated</c> as a raw deflate stream, no zlib wrapper, under <c>Content-Encoding: deflate</c>; the connection is kept.</summary>
public sealed class RawDeflateRoute() : EncodingRoute("/rawdeflate", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "deflate")], ServerPyCompression.RawDeflate(Ascii("raw deflated\n")));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RawDeflateRoute());
}
