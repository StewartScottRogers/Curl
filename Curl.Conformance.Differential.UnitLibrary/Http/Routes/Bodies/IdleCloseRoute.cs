using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// server.py's <c>/idle-close</c>: answers <c>idle\n</c> with <c>Connection: keep-alive</c> and
/// <c>Keep-Alive: timeout=1</c>, waits 0.3 seconds on the injected clock, then closes the connection anyway.
/// </summary>
public sealed class IdleCloseRoute : FixedPathRoute
{
    /// <summary>How long the route waits after answering before it closes.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc/>
    public override string Path => "/idle-close";

    /// <inheritdoc/>
    public override async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await base.RespondAsync(context, cancellationToken);
        await Task.Delay(Delay, context.Options.TimeProvider, cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Connection", "keep-alive"), ("Keep-Alive", "timeout=1")], "idle\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new IdleCloseRoute());
}
