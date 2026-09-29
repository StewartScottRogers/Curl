namespace Curl.Protocol.Ftp;

/// <summary>
/// Rewrites the connector's failure for a passive data connection into curl 8.21.0's form,
/// which names the control connection's host and port and then the data address after
/// <c>via</c>: <c>Failed to connect to 127.0.0.1:47707 via 127.0.0.2:1 after 2196 ms: Could
/// not connect to server</c> (measured, BL-904).
/// </summary>
/// <param name="DialedHost">The host the data connection was dialled on, which the connector's message names.</param>
/// <param name="ShownHost">The address curl names after <c>via</c>: the control connection's peer, or the <c>227</c> address under <c>--no-ftp-skip-pasv-ip</c>.</param>
/// <param name="Port">The data port.</param>
/// <param name="Control">The control connection's host and port, as its connect named them.</param>
internal sealed record FtpDataConnectFailure(string DialedHost, string ShownHost, int Port, FtpControlConnectionName Control)
{
    /// <summary>
    /// Names the control connection and the data address in a message that begins
    /// <c>Failed to connect to &lt;dialled host&gt;:&lt;port&gt; after </c>; any other message
    /// (a name that cannot be resolved, a proxy's failure) is returned as it is.
    /// </summary>
    /// <param name="message">The connector's message.</param>
    /// <returns>curl's message.</returns>
    internal string Rewrite(string message)
    {
        string dialed = $"Failed to connect to {DialedHost}:{Port} after ";
        return message.StartsWith(dialed, StringComparison.Ordinal)
            ? $"Failed to connect to {Control.Host}:{Control.Port} via {ShownHost}:{Port} after {message[dialed.Length..]}"
            : message;
    }
}
