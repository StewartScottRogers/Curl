using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/early</c>: 103 Early Hints with a <c>Link</c> preload, then 200 with <c>after 103\n</c>; the connection is kept.</summary>
public sealed class EarlyHintsRoute : InterimResponseRoute
{
    /// <inheritdoc/>
    public override string Path => "/early";

    /// <inheritdoc/>
    protected override byte[] InterimResponse => "HTTP/1.1 103 Early Hints\r\nLink: </s.css>; rel=preload\r\n\r\n"u8.ToArray();

    /// <inheritdoc/>
    protected override byte[] FinalBody => "after 103\n"u8.ToArray();

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new EarlyHintsRoute());
}
