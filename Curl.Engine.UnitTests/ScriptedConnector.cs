using System.Net;
using System.Security.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A connector whose one connection replays scripted server reads, one per
/// <see cref="IConnection.ReadAsync" />, splitting a scripted read longer than the caller's
/// buffer across reads as a socket would, then reports the server closing. Every connect
/// target and every byte written are recorded, so no socket is ever opened.
/// </summary>
internal sealed class ScriptedConnector(IEnumerable<byte[]> reads) : IConnector
{
    private readonly Queue<byte[]> pendingReads = new(reads);

    private readonly MemoryStream written = new();

    /// <summary>Gets each target connected to, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gets every byte written to any connection, in order.</summary>
    public byte[] Written => written.ToArray();

    /// <summary>Gets the protocol each connection's TLS handshake agreed with ALPN; <see langword="null" />, the default, for none.</summary>
    public string? ApplicationProtocol { get; init; }

    /// <summary>
    /// Gets a value indicating whether every connect after the first reports the connection reused
    /// from a pool, as a kept-alive connection is; <see langword="false" />, the default, for a new one each time.
    /// </summary>
    public bool ReusesTheFirstConnection { get; init; }

    /// <summary>
    /// Gets a value indicating whether each connect reports a TLS 1.3 handshake to the target's events,
    /// offering through ALPN what <see cref="Curl.Networking.TcpConnector" /> would: the target's
    /// <see cref="ConnectTarget.ApplicationProtocols" />, else <c>http/1.1</c> alone, the Windows build's
    /// offer with no version option; <see langword="false" />, the default, for none.
    /// </summary>
    public bool ReportsTheAlpnOffer { get; init; }

    /// <summary>
    /// Gets a value indicating whether each read throws <see cref="OperationCanceledException" /> once its token is
    /// cancelled, as a socket read does; <see langword="false" />, the default, to read on regardless.
    /// </summary>
    public bool ObservesCancellation { get; init; }

    /// <summary>
    /// Gets a value indicating whether each connect first reports to the target's events that it opened
    /// connection number 1, after its <c>Trying</c> info line, as <see cref="Curl.Networking.TcpConnector" /> does;
    /// <see langword="false" />, the default, for neither.
    /// </summary>
    public bool ReportsTheConnectionOpened { get; init; }

    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);
        if (ReportsTheConnectionOpened)
        {
            target.Events.ReportInfo($"  Trying 127.0.0.1:{target.Port}...");
            target.Events.ReportConnectionOpened(new ConnectionOpenedEvent
            {
                HostName = target.Host,
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, target.Port),
                LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
                IsSecondConnection = false,
                ConnectionNumber = 1,
            });
        }

        if (ReportsTheAlpnOffer)
        {
            target.Events.ReportTlsHandshake(new TlsHandshakeEvent
            {
                ProtocolVersion = SslProtocols.Tls13,
                CipherSuite = null,
                NegotiatedApplicationProtocol = ApplicationProtocol,
                OfferedApplicationProtocols = target.ApplicationProtocols ?? ["http/1.1"],
                ServerCertificate = null,
                CertificateVerified = false,
            });
        }

        bool isReused = ReusesTheFirstConnection && Targets.Count > 1;
        return ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection(pendingReads, written, ObservesCancellation), null, isReused: isReused, applicationProtocol: ApplicationProtocol));
    }

    private sealed class ScriptedConnection(Queue<byte[]> pendingReads, MemoryStream written, bool observesCancellation) : IConnection
    {
        private byte[] remainder = [];

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (observesCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (remainder.Length == 0 && !pendingReads.TryDequeue(out remainder!))
            {
                remainder = [];
                return ValueTask.FromResult(0);
            }

            int count = Math.Min(remainder.Length, buffer.Length);
            remainder.AsSpan(0, count).CopyTo(buffer.Span);
            remainder = remainder[count..];

            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
