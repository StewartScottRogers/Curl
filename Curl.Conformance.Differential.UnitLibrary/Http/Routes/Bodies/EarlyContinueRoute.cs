using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/early100</c>: an unasked-for 100 Continue carrying <c>X-Junk: 1</c>, then 200 with <c>after 100\n</c>; the connection is kept.</summary>
public sealed class EarlyContinueRoute : InterimResponseRoute
{
    /// <inheritdoc/>
    public override string Path => "/early100";

    /// <inheritdoc/>
    protected override byte[] InterimResponse => "HTTP/1.1 100 Continue\r\nX-Junk: 1\r\n\r\n"u8.ToArray();

    /// <inheritdoc/>
    protected override byte[] FinalBody => "after 100\n"u8.ToArray();

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new EarlyContinueRoute());
}
