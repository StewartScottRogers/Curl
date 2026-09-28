using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Opens channels through <paramref name="connector" /> and records each one it opens with
/// <paramref name="recorder" />: the channel's <see cref="IDatagramChannel.ServerEndPoint" /> as
/// the remote end and no local end, as curl 8.21.0 prints an empty <c>%{local_ip}</c> and a
/// <c>%{local_port}</c> of <c>0</c> for its unconnected TFTP socket (measured, BL-515 Notes;
/// ADR-0119).
/// </summary>
/// <param name="connector">Opens the channels.</param>
/// <param name="recorder">Remembers the first connection the transfer opens.</param>
internal sealed class EndPointRecordingDatagramConnector(IDatagramConnector connector, ConnectionEndPointRecorder recorder) : IDatagramConnector
{
    /// <summary>
    /// Opens a channel as the wrapped connector does and records it; a failed open
    /// records nothing.
    /// </summary>
    /// <param name="host">The server's host name or address.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open's result, unchanged.</returns>
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        DatagramOpenResult opened = await connector.OpenAsync(host, port, cancellationToken).ConfigureAwait(false);
        if (opened.Channel is { } channel)
        {
            recorder.Record(null, channel.ServerEndPoint as IPEndPoint);
        }

        return opened;
    }
}
