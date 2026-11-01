using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// The connection a <see cref="WsProtocolHandler" /> transfer that exchanges frames is on, from the <c>101</c> until
/// the transfer ends, so libcurl's <c>curl_ws_send</c> called from inside the transfer's write callback writes to it,
/// as curl 8.22.0's does on <c>data-&gt;conn</c> during an ordinary perform (upstream test2301, BL-2710); and the frame
/// of the payload being written, so <c>curl_ws_meta</c> called from the write callback answers it, as libcurl's
/// <c>ws->recvframe</c> (upstream test2302, BL-2711).
/// </summary>
public sealed class WsPerformConnection
{
    /// <summary>Gets the connection the transfer is exchanging frames on, or <see langword="null" /> outside one.</summary>
    public IConnection? Connection { get; private set; }

    /// <summary>
    /// Gets the run of frame payload last handed to the output, its <see cref="WsFrameRun.Payload" /> the bytes of that
    /// write, or <see langword="null" /> before any since the <c>101</c> and under <c>CURLWS_RAW_MODE</c>.
    /// </summary>
    public WsFrameRun? Frame { get; private set; }

    /// <summary>
    /// Gets the opcode of the data message <see cref="Frame" /> is part of: the last <see cref="WsOpcode.Text" /> or
    /// <see cref="WsOpcode.Binary" /> frame written, which a <see cref="WsOpcode.Continuation" /> frame continues;
    /// <see cref="WsOpcode.Continuation" /> before any.
    /// </summary>
    public WsOpcode MessageOpcode { get; private set; }

    /// <summary>Marks the transfer's frames as flowing on <paramref name="connection" />, with no frame written yet.</summary>
    /// <param name="connection">The upgraded connection.</param>
    internal void Open(IConnection connection)
    {
        Connection = connection;
        Frame = null;
        MessageOpcode = WsOpcode.Continuation;
    }

    /// <summary>Keeps <paramref name="run" /> as the frame of the payload about to be written.</summary>
    /// <param name="run">The run of payload.</param>
    internal void Writing(WsFrameRun run)
    {
        Frame = run;
        if (run.Opcode is WsOpcode.Text or WsOpcode.Binary)
        {
            MessageOpcode = run.Opcode;
        }
    }

    /// <summary>Marks the transfer as done with its connection.</summary>
    internal void Close() => Connection = null;
}
