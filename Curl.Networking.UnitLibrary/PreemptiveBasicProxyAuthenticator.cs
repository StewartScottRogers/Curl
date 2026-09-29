using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Answers a CONNECT with <c>Proxy-Authorization: Basic</c> before any challenge, and never
/// after one: what curl 8.21.0 sends with <c>-U</c> and no <c>--proxy-*</c> auth switch, and
/// the <see cref="HttpProxyTunnelOptions.ProxyAuthenticator" /> a tunnel uses unless the
/// composition gives it one that answers challenges too.
/// </summary>
/// <param name="credentialEncoding">The encoding <c>user:password</c> is turned into bytes with before it is base64-encoded.</param>
internal sealed class PreemptiveBasicProxyAuthenticator(Encoding credentialEncoding) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count == 0 && request.AllowedSchemes == HttpAuthSchemes.Basic && request.Credential is { } credential
            ? $"Basic {Convert.ToBase64String(credentialEncoding.GetBytes($"{credential.UserName}:{credential.Password}"))}"
            : null;
}
