using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// server.py's <c>/status/&lt;n&gt;</c>: answers status n with <c>status n\n</c> and keeps the connection;
/// a segment that is not a number ends the connection unanswered, as server.py's exception does.
/// </summary>
public sealed class StatusCodeRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path.StartsWith("/status/", StringComparison.Ordinal);

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var segment = context.Request.Path.Split('/')[2];
        if (!int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
        {
            return true;
        }

        var body = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"status {status}\n"));
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection, new DifferentialHttpResponse(status, [("Content-Type", "text/plain")], body), cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusCodeRoute());
}
