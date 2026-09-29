using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that, as <c>NegotiateHttpAuthenticator</c> does for a
/// context that makes no token (ADR-0231), reports a failure line to
/// <see cref="HttpAuthRequest.Events" /> on every asynchronous call and returns
/// <paramref name="authorization" />.
/// </summary>
/// <param name="failureLine">The line reported on each call.</param>
/// <param name="authorization">The value returned, or <see langword="null" /> for no header.</param>
public sealed class FailingNegotiateAuthenticator(string failureLine, string? authorization = null) : IHttpAuthenticator
{
    /// <summary>Gets the requests asked about, in order.</summary>
    public List<HttpAuthRequest> Requests { get; } = [];

    /// <summary>Gets the challenge lists passed, in order.</summary>
    public List<IReadOnlyList<string>> Challenges { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) => null;

    /// <inheritdoc />
    public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Challenges.Add(challenges);
        request.Events.ReportInfo(failureLine);
        return ValueTask.FromResult(authorization);
    }
}
