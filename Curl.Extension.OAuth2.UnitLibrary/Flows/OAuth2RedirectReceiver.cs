using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// Accepts the one connection the browser makes to the loopback redirect URI (RFC 8252 §7.3), reads its request, and
/// answers with a short page telling the user to go back to curl.
/// </summary>
internal static class OAuth2RedirectReceiver
{
    /// <summary>The most bytes of request head read; a longer one is read only this far.</summary>
    public const int LongestRequestHead = 16 * 1024;

    public const string AnsweredPage = "curl has the sign-in response. You can close this window.\n";

    public const string RefusedPage = "curl could not read this request.\n";

    /// <summary>Accepts the redirect on <paramref name="pending"/>.</summary>
    /// <returns>The redirect, or the failure: the accept's exit code, or <see cref="CurlExitCode.WeirdServerReply"/> for a request that is not a GET.</returns>
    public static async Task<(OAuth2Redirect? Redirect, OAuth2FlowResult? Failure)> ReceiveAsync(
        IPendingConnection pending, CancellationToken cancellationToken)
    {
        var accepted = await pending.AcceptAsync(cancellationToken);
        if (accepted.Connection is not { } connection)
        {
            return (null, OAuth2FlowResult.Failed(accepted.ExitCode, accepted.ErrorMessage!));
        }

        await using (connection)
        {
            var requestLine = await ReadRequestLineAsync(connection, cancellationToken);
            var redirect = requestLine is null ? null : OAuth2Redirect.ParseRequestLine(requestLine);
            await AnswerAsync(connection, redirect is not null, cancellationToken);
            return redirect is null
                ? (null, OAuth2FlowResult.Failed(CurlExitCode.WeirdServerReply, "The redirect to curl was not an HTTP GET request."))
                : (redirect, null);
        }
    }

    private static async Task<string?> ReadRequestLineAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[LongestRequestHead];
        var filled = 0;
        while (filled < buffer.Length && buffer.AsSpan(0, filled).IndexOf("\r\n\r\n"u8) < 0)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        var end = buffer.AsSpan(0, filled).IndexOf("\r\n"u8);
        return end < 0 ? null : Encoding.Latin1.GetString(buffer, 0, end);
    }

    private static async Task AnswerAsync(IConnection connection, bool understood, CancellationToken cancellationToken)
    {
        var (status, page) = understood ? ("200 OK", AnsweredPage) : ("400 Bad Request", RefusedPage);
        var response = $"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {page.Length}\r\nConnection: close\r\n\r\n{page}";
        await connection.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        await connection.FlushAsync(cancellationToken);
    }
}
