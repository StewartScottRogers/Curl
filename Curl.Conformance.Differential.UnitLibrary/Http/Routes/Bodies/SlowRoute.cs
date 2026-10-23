using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/slow</c>: waits 3 seconds on the injected clock, then answers <c>slow\n</c>; the connection is kept.</summary>
public sealed class SlowRoute : FixedPathRoute
{
    /// <summary>How long the route waits before answering.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <inheritdoc/>
    public override string Path => "/slow";

    /// <inheritdoc/>
    public override async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Delay, context.Options.TimeProvider, cancellationToken);
        return await base.RespondAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) => new(200, [], "slow\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new SlowRoute());
}
