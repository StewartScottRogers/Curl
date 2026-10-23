using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/identity</c>: a plain body under <c>Content-Encoding: identity</c>; the connection is kept.</summary>
public sealed class IdentityRoute() : EncodingRoute("/identity", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "identity")], Ascii("ident\n"));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new IdentityRoute());
}
