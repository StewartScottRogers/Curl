namespace Curl.Protocol.Abstractions;

/// <summary>
/// Turns a <see cref="ConnectTarget" /> into an <see cref="IConnection" />: the seam a
/// byte-stream protocol handler acquires its transport through, once per transfer.
/// </summary>
/// <remarks>
/// <para>
/// A handler takes a connector in its constructor and asks it for a connection from the
/// host and port in each transfer's URL, because the handler is registered once and
/// exists before any URL does (ADR-0005). The handler owns and disposes the
/// <see cref="IConnection" /> it is given, and never constructs a
/// <see cref="System.Net.Sockets.Socket" /> or <c>SslStream</c> itself.
/// </para>
/// <para>
/// Every protocol but TFTP uses this seam; TFTP uses <see cref="IDatagramConnector" />.
/// A protocol that negotiates a second connection mid-session, such as FTP's data
/// connection, calls the same connector again with the negotiated host and port.
/// </para>
/// </remarks>
public interface IConnector
{
    /// <summary>
    /// Connects to <paramref name="target" />, wrapping the connection in TLS when
    /// <see cref="ConnectTarget.UseTls" /> is <see langword="true" />.
    /// </summary>
    /// <param name="target">The host, port and TLS choice to connect with.</param>
    /// <param name="cancellationToken">Cancels the resolve, connect and handshake.</param>
    /// <returns>
    /// <see cref="ConnectResult.Connected(IConnection)" /> with the open connection, or
    /// <see cref="ConnectResult.Failed(CurlExitCode, string)" /> carrying curl's exit code
    /// and the message curl prints: <see cref="CurlExitCode.CouldntResolveHost" /> (6)
    /// when the host does not resolve, <see cref="CurlExitCode.CouldntConnect" /> (7) when
    /// no connection can be made, and curl's TLS exit code when the handshake fails.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape; every resolve, connect or TLS failure is returned as
    /// a failed result instead.
    /// </exception>
    ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken);
}
