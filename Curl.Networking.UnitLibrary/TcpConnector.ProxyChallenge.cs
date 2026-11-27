using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    /// <summary>
    /// The <c>Proxy-Authorization</c> that answers <paramref name="reply" />, and whether it goes
    /// on the same connection, its body discarded: none unless the reply is a <c>407</c> the
    /// authenticator answers - afresh when the CONNECT sent nothing, and through
    /// <see cref="IHttpAuthenticator.ContinueAuthorizationAsync" /> when it sent a credential, which
    /// only NTLM and Negotiate go on from, so curl 8.21.0's giving up on Basic or Digest sent and
    /// challenged again stays. An empty answer (<c>--proxy-anyauth</c> picking a Negotiate
    /// context that makes no token) sends nothing more. A failure the authenticator reports,
    /// such as an NTLM Type 2 SSPI cannot answer, is the tunnel's failure with its exit code.
    /// </summary>
    private async ValueTask<(string? Answer, bool OnThisConnection, ConnectResult? Failure)> AnswerProxyChallengeAsync(
        IConnection connection,
        HttpProxyTunnelReply reply,
        TunnelRequest tunnel,
        string? sentAuthorization,
        bool sentAnswersChallenge,
        CancellationToken cancellationToken)
    {
        if (reply.StatusCode != 407)
        {
            EndProxyAuthorization(sentAuthorization);
            return (null, false, null);
        }

        var (answer, authenticationFailure) = await AskAuthenticatorToAnswer407Async(reply, tunnel, sentAuthorization, sentAnswersChallenge, cancellationToken).ConfigureAwait(false);
        if (authenticationFailure is not null)
        {
            return (null, false, authenticationFailure);
        }

        if (string.IsNullOrEmpty(answer))
        {
            // curl 8.21.0 reads an unanswered 407's chunked body before it gives up, so a
            // malformed one is exit 56, not exit 7 (measured, upstream test1715, BL-1857).
            var unreadable = reply.IsChunked ? await DiscardChunkedBodyAsync(connection, tunnel.Target.Events, cancellationToken).ConfigureAwait(false) : null;
            return (null, false, unreadable);
        }

        return reply.LeavesConnectionReusable
            ? await DiscardRejectedBodyAsync(connection, reply, answer, tunnel.Target.Events, cancellationToken).ConfigureAwait(false)
            : (answer, false, null);
    }

    /// <summary>
    /// Asks the authenticator for the <c>Proxy-Authorization</c> that answers the <c>407</c>
    /// <paramref name="reply" />: afresh when the CONNECT sent none or the reply marks its Digest
    /// nonce stale, else as the next leg of the handshake behind
    /// <paramref name="sentAuthorization" />. A failure the authenticator reports is returned as
    /// the tunnel's failure with its exit code.
    /// </summary>
    private async ValueTask<(string? Answer, ConnectResult? Failure)> AskAuthenticatorToAnswer407Async(
        HttpProxyTunnelReply reply,
        TunnelRequest tunnel,
        string? sentAuthorization,
        bool sentAnswersChallenge,
        CancellationToken cancellationToken)
    {
        try
        {
            var answer = sentAuthorization is null || AnswersStaleDigest(sentAuthorization, reply)
                ? await CreateProxyAuthorizationAsync(tunnel.Destination, tunnel.Proxy, reply.ProxyAuthenticate, tunnel.AuthenticatorEvents, cancellationToken).ConfigureAwait(false)
                : await ProxyAuthenticator.ContinueAuthorizationAsync(ProxyAuthRequestOf(tunnel.Destination, tunnel.Proxy) with { Events = tunnel.AuthenticatorEvents }, sentAuthorization, !sentAnswersChallenge, reply.ProxyAuthenticate, cancellationToken).ConfigureAwait(false);
            return (answer, null);
        }
        catch (HttpAuthenticationFailedException failure)
        {
            return (null, ConnectResult.Failed(failure.ExitCode, failure.Message));
        }
    }

    /// <summary>
    /// Decides whether <paramref name="reply" /> marks the nonce of the Digest answer
    /// <paramref name="sentAuthorization" /> stale, so it is answered afresh with the new nonce,
    /// as curl 8.21.0 does (BL-864 Notes).
    /// </summary>
    private static bool AnswersStaleDigest(string sentAuthorization, HttpProxyTunnelReply reply) =>
        sentAuthorization.StartsWith("Digest ", StringComparison.Ordinal) && DigestStaleChallenge.IsOfferedIn(reply.ProxyAuthenticate);

    /// <summary>
    /// Discards the body of a reply that leaves the connection reusable, so
    /// <paramref name="answer" /> goes on it: a <c>Content-Length</c> body that ends early sends
    /// the answer on a new connection instead, and a chunked body that is malformed or cut
    /// short is the tunnel's exit 56, as curl 8.21.0 gives up on it (BL-862 Notes). The body's
    /// <c>-v</c> lines go to <paramref name="events" /> as <see cref="ConnectTunnelVerboseLines" />
    /// words them (BL-1144, BL-1146).
    /// </summary>
    private static async ValueTask<(string? Answer, bool OnThisConnection, ConnectResult? Failure)> DiscardRejectedBodyAsync(
        IConnection connection,
        HttpProxyTunnelReply reply,
        string answer,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        if (!reply.IsChunked)
        {
            ConnectTunnelVerboseLines.ReportIgnoredBody(events, reply.ContentLength);
            return (answer, await HttpProxyTunnel.DiscardBodyAsync(connection, reply.ContentLength, cancellationToken).ConfigureAwait(false), null);
        }

        var failure = await DiscardChunkedBodyAsync(connection, events, cancellationToken).ConfigureAwait(false);
        return failure is null
            ? (answer, true, null)
            : (null, false, failure);
    }

    /// <summary>
    /// Discards a reply's chunked body with its <c>-v</c> lines, returning <see langword="null" />
    /// once it has ended, else the exit 56 failure curl 8.21.0 gives up with (BL-862 Notes).
    /// </summary>
    private static async ValueTask<ConnectResult?> DiscardChunkedBodyAsync(IConnection connection, ITransferEvents events, CancellationToken cancellationToken)
    {
        events.ReportInfo(ConnectTunnelVerboseLines.IgnoreChunkedBody);
        var failure = await HttpProxyTunnel.DiscardChunkedBodyAsync(connection, cancellationToken).ConfigureAwait(false);
        ConnectTunnelVerboseLines.ReportChunkedBodyEnd(events, failure);
        return failure is null ? null : ConnectResult.Failed(CurlExitCode.RecvError, failure);
    }
}
