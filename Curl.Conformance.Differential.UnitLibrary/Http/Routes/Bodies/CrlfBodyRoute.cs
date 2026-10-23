using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/crlf-body</c>: a body mixing CRLF and LF line ends with a Ctrl-Z byte; the connection is kept.</summary>
public sealed class CrlfBodyRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/crlf-body";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [], "line1\r\nline2\nline3\r\n\x1a end\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new CrlfBodyRoute());
}
