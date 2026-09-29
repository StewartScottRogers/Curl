using System.Net.Security;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>Maps the BCL's <see cref="NegotiateAuthenticationStatusCode" /> onto <see cref="SecurityContextStatus" /> (ADR-0142).</summary>
internal static class NegotiateAuthenticationStatusMapping
{
    private static readonly Dictionary<NegotiateAuthenticationStatusCode, SecurityContextStatus> Statuses = new()
    {
        [NegotiateAuthenticationStatusCode.Completed] = SecurityContextStatus.Completed,
        [NegotiateAuthenticationStatusCode.ContinueNeeded] = SecurityContextStatus.ContinueNeeded,
        [NegotiateAuthenticationStatusCode.Unsupported] = SecurityContextStatus.NoMechanism,
        [NegotiateAuthenticationStatusCode.UnknownCredentials] = SecurityContextStatus.NoCredentials,
        [NegotiateAuthenticationStatusCode.CredentialsExpired] = SecurityContextStatus.NoCredentials,
        [NegotiateAuthenticationStatusCode.InvalidToken] = SecurityContextStatus.MalformedToken,
        [NegotiateAuthenticationStatusCode.MessageAltered] = SecurityContextStatus.MalformedToken,
    };

    /// <summary>Gets the status <paramref name="code" /> comes to.</summary>
    /// <param name="code">What <see cref="NegotiateAuthentication.GetOutgoingBlob(ReadOnlySpan{byte}, out NegotiateAuthenticationStatusCode)" /> said.</param>
    /// <returns>
    /// <see cref="SecurityContextStatus.NoMechanism" /> for <c>Unsupported</c>, the runtime's
    /// answer where no system GSS-API library or package exists, so the router falls back to
    /// the hand-built route; <see cref="SecurityContextStatus.NoCredentials" /> for unknown or
    /// expired credentials; <see cref="SecurityContextStatus.MalformedToken" /> for a token it
    /// cannot read; <see cref="SecurityContextStatus.Refused" /> for anything else.
    /// </returns>
    public static SecurityContextStatus StatusOf(NegotiateAuthenticationStatusCode code) =>
        Statuses.GetValueOrDefault(code, SecurityContextStatus.Refused);
}
