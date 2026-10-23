using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// A server.py route that sends one fixed interim response, raw and without a <c>Date</c>, then a
/// <c>resp</c> answer 200 with its body; the connection is kept.
/// </summary>
public abstract class InterimResponseRoute : FixedPathRoute
{
    /// <summary>Gets the raw interim response sent first.</summary>
    protected abstract byte[] InterimResponse { get; }

    /// <summary>Gets the body of the final 200 answer.</summary>
    protected abstract byte[] FinalBody { get; }

    /// <inheritdoc/>
    public override async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteRawAsync(context.Connection, InterimResponse, cancellationToken);
        return await base.RespondAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) => new(200, [], FinalBody);
}
