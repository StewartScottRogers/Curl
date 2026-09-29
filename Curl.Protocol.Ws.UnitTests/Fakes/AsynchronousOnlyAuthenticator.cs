using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that, as Negotiate does in
/// <c>RankedHttpAuthenticator</c>, answers only through
/// <see cref="IHttpAuthenticator.CreateAuthorizationAsync" />: the synchronous call sends
/// nothing, so a handler still calling it sends no header (ADR-0176, ADR-0226).
/// </summary>
/// <param name="authorization">The value the asynchronous call returns.</param>
public sealed class AsynchronousOnlyAuthenticator(string authorization) : IHttpAuthenticator
{
    /// <summary>Gets the requests the asynchronous call was asked about, in order.</summary>
    public List<HttpAuthRequest> Requests { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) => null;

    /// <inheritdoc />
    public ValueTask<string?> CreateAuthorizationAsync(HttpAuthRequest request, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return ValueTask.FromResult<string?>(authorization);
    }
}
