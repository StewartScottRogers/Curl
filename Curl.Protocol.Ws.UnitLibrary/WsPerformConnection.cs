using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// The connection a <see cref="WsProtocolHandler" /> transfer that exchanges frames is on, from the <c>101</c> until
/// the transfer ends, so libcurl's <c>curl_ws_send</c> called from inside the transfer's write callback writes to it,
/// as curl 8.22.0's does on <c>data-&gt;conn</c> during an ordinary perform (upstream test2301, BL-2710).
/// </summary>
public sealed class WsPerformConnection
{
    /// <summary>Gets the connection the transfer is exchanging frames on, or <see langword="null" /> outside one.</summary>
    public IConnection? Connection { get; private set; }

    /// <summary>Marks the transfer's frames as flowing on <paramref name="connection" />.</summary>
    /// <param name="connection">The upgraded connection.</param>
    internal void Open(IConnection connection) => Connection = connection;

    /// <summary>Marks the transfer as done with its connection.</summary>
    internal void Close() => Connection = null;
}
