using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Where curl writes the <c>-v</c> lines its NTLM handshake reports while it reads a challenge
/// (<c>NTLM handshake rejected</c>, <c>NTLM authentication problem, ignoring.</c> and the like):
/// just before the <c>WWW-Authenticate</c> header of a 401 that offers NTLM, as it reads the
/// challenge there (measured with curl 8.21.0 and 8.18.0, BL-848 Notes), and so, by the same
/// code in curl, before a 407's <c>Proxy-Authenticate</c> header for a proxy's handshake.
/// <see cref="AuthRestartedLine" /> goes in the same place.
/// </summary>
internal static class HttpNtlmInfoLines
{
    /// <summary>
    /// The line curl 8.22.0 writes for a bare challenge that starts a handshake again
    /// (<see cref="RestartsAuth" />, measured, BL-5932).
    /// </summary>
    internal const string AuthRestartedLine = "NTLM auth restarted";

    /// <summary>The scheme's name, as a challenge starts with it.</summary>
    private const string SchemeName = "NTLM";

    /// <summary>
    /// Decides whether <paramref name="header" /> is a bare <c>NTLM</c> challenge to a request
    /// that sent <paramref name="request" />'s side no value because that side's Type 3 went out
    /// in an earlier request, which libcurl's <c>Curl_input_ntlm</c> answers by starting the
    /// handshake again. A challenge is bare when <c>NTLM</c> ends the header's value.
    /// </summary>
    /// <param name="request">The request as the authenticator is asked about it, for the origin or the proxy.</param>
    /// <param name="ntlmType3WentOut"><see langword="true" /> when the side's Type 3 went out in an earlier request.</param>
    /// <param name="statusLine">The final head's status line.</param>
    /// <param name="header">The header.</param>
    /// <returns><see langword="true" /> when <see cref="AuthRestartedLine" /> belongs before this header.</returns>
    internal static bool RestartsAuth(HttpAuthRequest request, bool ntlmType3WentOut, HttpStatusLine statusLine, HttpResponseHeader header) =>
        ntlmType3WentOut
            && IsNtlmChallenge(request, statusLine, header)
            && string.Equals(header.Value.Split(',')[^1].Trim(' ', '\t'), SchemeName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides whether <paramref name="header" /> of a final head is where the lines answering
    /// its challenges for <paramref name="request" /> belong: a 401's <c>WWW-Authenticate</c>, or
    /// for a proxy's request a 407's <c>Proxy-Authenticate</c>, offering NTLM when NTLM is allowed.
    /// </summary>
    /// <param name="request">The request as the authenticator is asked about it, for the origin or the proxy.</param>
    /// <param name="statusLine">The final head's status line.</param>
    /// <param name="header">The header.</param>
    /// <returns><see langword="true" /> when the authenticator's lines belong before this header.</returns>
    internal static bool IsNtlmChallenge(HttpAuthRequest request, HttpStatusLine statusLine, HttpResponseHeader header) =>
        statusLine.StatusCode == ChallengeStatusCodeOf(request)
            && (request.AllowedSchemes & HttpAuthSchemes.Ntlm) != 0
            && string.Equals(header.Name, ChallengeHeaderNameOf(request), StringComparison.OrdinalIgnoreCase)
            && header.Value.Split(',').Any(OffersNtlm);

    /// <summary>Gives the status that challenges <paramref name="request" />: 407 for a proxy's, 401 for the origin's.</summary>
    private static int ChallengeStatusCodeOf(HttpAuthRequest request) => request.IsProxy ? 407 : 401;

    /// <summary>Gives the header that carries <paramref name="request" />'s challenges.</summary>
    private static string ChallengeHeaderNameOf(HttpAuthRequest request) => request.IsProxy ? "Proxy-Authenticate" : "WWW-Authenticate";

    /// <summary>Decides whether a challenge, after its leading blanks, starts with the scheme <c>NTLM</c>, in any case.</summary>
    private static bool OffersNtlm(string element)
    {
        string challenge = element.TrimStart(' ', '\t');
        return challenge.StartsWith(SchemeName, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == SchemeName.Length || challenge[SchemeName.Length] is ' ' or '\t');
    }
}
