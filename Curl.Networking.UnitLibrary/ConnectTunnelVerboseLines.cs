using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0 (Schannel) writes around each CONNECT to an HTTP proxy, as
/// measured with <c>Record-CurlExchange.ps1</c> as the proxy (BL-863 Notes): <c>Proxy auth using
/// &lt;scheme&gt; with user '&lt;user&gt;'</c> and <c>Establishing HTTP proxy tunnel to
/// &lt;host&gt;:&lt;port&gt;</c> before it, the request head, each reply header line, and after a
/// <c>407</c>'s <c>Proxy-Authenticate</c> line, when the CONNECT sent a Basic or Digest value,
/// <c>&lt;scheme&gt; authentication problem, ignoring.</c> once for each challenge offering that
/// scheme.
/// </summary>
internal static class ConnectTunnelVerboseLines
{
    /// <summary>The line curl writes before dialling the proxy again after a <c>407</c> that closed the connection.</summary>
    internal const string ConnectAgain = "Connect me again please";

    /// <summary>
    /// Reports the lines curl writes before a CONNECT to <paramref name="request" />'s target: the
    /// <c>Proxy auth using</c> line when a scheme is picked, then <c>Establishing HTTP proxy tunnel to</c>.
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="request">The CONNECT as the proxy's authenticator is asked about it.</param>
    /// <param name="authorization">The <c>Proxy-Authorization</c> value the CONNECT sends, or <see langword="null" />.</param>
    /// <param name="answersChallenge"><see langword="true" /> when the CONNECT answers a <c>407</c>.</param>
    internal static void ReportBeforeConnect(ITransferEvents events, HttpAuthRequest request, string? authorization, bool answersChallenge)
    {
        if (PickedScheme(request, authorization, answersChallenge) is { } scheme)
        {
            events.ReportInfo($"Proxy auth using {scheme} with user '{request.Credential?.UserName}'");
        }

        events.ReportInfo($"Establishing HTTP proxy tunnel to {request.RequestTarget}");
    }

    /// <summary>
    /// Reports each line of the reply head <paramref name="head" /> as a response header, and after
    /// a <c>407</c>'s <c>Proxy-Authenticate</c> line the <c>authentication problem</c> lines for
    /// the Basic or Digest value <paramref name="authorization" /> the CONNECT sent.
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="head">The reply head, its final blank line included.</param>
    /// <param name="statusCode">The reply's status code.</param>
    /// <param name="authorization">The <c>Proxy-Authorization</c> value the CONNECT sent, or <see langword="null" />.</param>
    internal static void ReportReplyHead(ITransferEvents events, ReadOnlySpan<byte> head, int statusCode, string? authorization)
    {
        var refusedScheme = statusCode == 407 ? RefusedScheme(authorization) : null;
        while (!head.IsEmpty)
        {
            var lineFeed = head.IndexOf((byte)'\n');
            var end = lineFeed < 0 ? head.Length : lineFeed + 1;
            var line = head[..end];
            events.ReportResponseHeader(line);
            if (refusedScheme is not null)
            {
                ReportProblemLines(events, Encoding.Latin1.GetString(line), refusedScheme);
            }

            head = head[end..];
        }
    }

    private static void ReportProblemLines(ITransferEvents events, string line, string scheme)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || !string.Equals(line[..colon], "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var challenge in line[(colon + 1)..].Split(','))
        {
            if (Offers(challenge.Trim(), scheme))
            {
                events.ReportInfo($"{scheme} authentication problem, ignoring.");
            }
        }
    }

    /// <summary>
    /// The scheme curl names before a CONNECT: Digest for a first CONNECT with Digest the one
    /// scheme allowed and a user, which sends no value yet; else the scheme the value starts with
    /// when it is Basic, Digest or NTLM; else none.
    /// </summary>
    private static string? PickedScheme(HttpAuthRequest request, string? authorization, bool answersChallenge) =>
        authorization is null
            ? PicksDigestBeforeAnyValue(request, answersChallenge) ? "Digest" : null
            : NamedScheme(SchemeOf(authorization));

    private static bool PicksDigestBeforeAnyValue(HttpAuthRequest request, bool answersChallenge) =>
        !answersChallenge && request.AllowedSchemes == HttpAuthSchemes.Digest && request.Credential is not null;

    private static string? NamedScheme(string scheme) => scheme is "Basic" or "Digest" or "NTLM" ? scheme : null;

    /// <summary>The scheme a sent value starts with when a <c>407</c> to it is given up on: Basic or Digest; else none.</summary>
    private static string? RefusedScheme(string? authorization) =>
        authorization is not null && SchemeOf(authorization) is "Basic" or "Digest" ? SchemeOf(authorization) : null;

    private static string SchemeOf(string authorization) => authorization.Split(' ', 2)[0];

    /// <summary>
    /// Decides whether a challenge starts with <paramref name="scheme" />, in any case, followed by
    /// its end or white space, as curl's <c>is_valid_auth_separator</c> does.
    /// </summary>
    private static bool Offers(string challenge, string scheme) =>
        challenge.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == scheme.Length || char.IsWhiteSpace(challenge[scheme.Length]));
}
