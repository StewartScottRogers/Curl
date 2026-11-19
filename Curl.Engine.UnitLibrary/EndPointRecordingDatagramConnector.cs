using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Opens channels through <paramref name="connector" /> and records each one it opens with
/// <paramref name="recorder" />: the channel's <see cref="IDatagramChannel.ServerEndPoint" /> as
/// the remote end and no local end, as curl 8.21.0 prints an empty <c>%{local_ip}</c> and a
/// <c>%{local_port}</c> of <c>0</c> for its unconnected TFTP socket (measured, BL-515 Notes;
/// ADR-0119).
/// </summary>
/// <param name="connector">Opens the channels.</param>
/// <param name="recorder">Remembers the first connection the transfer opens.</param>
/// <remarks>A channel's open time is stamped with the recorder's <see cref="ConnectionEndPointRecorder.TransferClock" />, the transfer's own clock (BL-5535).</remarks>
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
        if (opened.Channel is not { } channel)
        {
            return opened;
        }

        // Only the channel that is both the first datagram channel and the first connection owns the
        // recorded end points, so only it is wrapped to re-record them (BL-5536). Both recordings run.
        bool firstConnection = recorder.Record(null, channel.ServerEndPoint as IPEndPoint);
        bool firstChannel = recorder.RecordDatagramChannel(channel.LocalEndPoint as IPEndPoint, recorder.TransferClock.GetTimestamp());
        return firstConnection & firstChannel ? DatagramOpenResult.Opened(new ReRecordingChannel(channel, recorder)).WithConnectionNumber(opened.ConnectionNumber) : opened;
    }

    /// <summary>
    /// Wrapped only around the transfer's first datagram channel when it is also its first recorded
    /// connection (BL-5536). Forwards to <paramref name="inner" /> and, once a datagram has been received, records its end
    /// points again: a channel that moved to the next resolved address after a refusal (EXT-100) carries
    /// the transfer from another socket than the one it opened with (BL-5532).
    /// </summary>
    private sealed class ReRecordingChannel(IDatagramChannel inner, ConnectionEndPointRecorder recorder) : IDatagramChannel
    {
        private bool _recorded;

        public EndPoint ServerEndPoint => inner.ServerEndPoint;

        public EndPoint? LocalEndPoint => inner.LocalEndPoint;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            inner.SendAsync(datagram, destination, cancellationToken);

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            DatagramReceived received = await inner.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (!_recorded)
            {
                _recorded = true;
                recorder.ReRecordDatagramChannel(inner.ServerEndPoint as IPEndPoint, inner.LocalEndPoint as IPEndPoint);
            }

            return received;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
