using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Transfer events that also take the head of each reply an HTTP proxy sends to
/// <c>CONNECT</c>, for the transfer's header output: curl 8.21.0 writes every such head,
/// the status line to the blank line, to <c>-i</c>, <c>-I</c> and <c>-D</c> before the
/// response's own, unless <c>--suppress-connect-headers</c> is given (measured
/// 2026-09-30, BL-613 Notes).
/// </summary>
/// <remarks>
/// <see cref="TcpConnector" /> looks for this interface on <see cref="ConnectTarget.Events" />;
/// events without it have no header output to write to, and the head goes nowhere.
/// </remarks>
public interface IConnectReplyHeadWritingEvents : ITransferEvents
{
    /// <summary>
    /// Writes one complete CONNECT reply head, exactly as received, to the header output.
    /// </summary>
    /// <param name="head">The head's bytes, from its status line to its blank line, line ends included.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the head is written.</returns>
    ValueTask WriteConnectReplyHeadAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken);
}
