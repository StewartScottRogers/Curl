using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Encodings;

/// <summary>server.py's <c>/unknownenc</c>: a body under the unknown <c>Content-Encoding: foo</c>; the connection is kept.</summary>
public sealed class UnknownEncodingRoute() : EncodingRoute("/unknownenc", closesConnection: false)
{
    /// <inheritdoc/>
    protected override byte[] ResponseBytes() =>
        OkResponse([("Content-Encoding", "foo")], Ascii("data"));

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new UnknownEncodingRoute());
}
