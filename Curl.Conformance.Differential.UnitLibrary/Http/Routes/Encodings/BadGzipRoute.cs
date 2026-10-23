using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/badgzip</c>: plain text under <c>Content-Encoding: gzip</c>; the connection is kept.</summary>
public sealed class BadGzipRoute() : EncodingRoute("/badgzip", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "gzip")], Ascii("not gzip at all"));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new BadGzipRoute());
}
