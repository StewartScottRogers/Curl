using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that never sends a header: the stand-in until the
/// real authenticators exist.
/// </summary>
public sealed class SilentAuthenticator : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) => null;
}
