using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The events of a <see cref="TcpConnector" /> connect run again for the hand-built client
/// (ADR-1459): everything the first attempt already reported, up to the trust of the handshake that
/// asked for the redial, is held back, so <c>-v</c> shows one connection's lines, as curl writes
/// them (ADR-1460).
/// </summary>
/// <remarks>
/// The <see cref="ReportTlsTrust" /> of the handshake that asked for the redial, the one after
/// <paramref name="earlierTrusts" /> others, ends the holding back; it is held back itself, and
/// everything after it passes. A connect that ends before then failed on the way to that
/// handshake, so <see cref="ReportHeldLines" /> writes the lines held, which explain the failure;
/// the other events held back are dropped.
/// </remarks>
/// <param name="events">The connect's own events.</param>
/// <param name="earlierTrusts">
/// How many handshakes report their trust before the one that asked for the redial: one for an
/// origin reached through an HTTPS proxy, whose handshake comes first, none otherwise.
/// </param>
internal sealed class RedialedConnectTransferEvents(ITransferEvents events, int earlierTrusts) : IConnectReplyHeadWritingEvents
{
    private readonly List<(bool IsWarning, string Text)> _heldLines = [];
    private int _trustsToHoldBack = earlierTrusts;
    private bool _reachedHandshake;

    /// <summary>
    /// Writes the lines held back, in order, when the connect ended before the redialled
    /// handshake's trust was reported; nothing otherwise.
    /// </summary>
    public void ReportHeldLines()
    {
        _reachedHandshake = true;
        foreach (var (isWarning, text) in _heldLines)
        {
            if (isWarning)
            {
                events.ReportWarning(text);
            }
            else
            {
                events.ReportInfo(text);
            }
        }

        _heldLines.Clear();
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (_reachedHandshake)
        {
            events.ReportInfo(text);
            return;
        }

        _heldLines.Add((false, text));
    }

    /// <inheritdoc />
    public void ReportWarning(string text)
    {
        if (_reachedHandshake)
        {
            events.ReportWarning(text);
            return;
        }

        _heldLines.Add((true, text));
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust)
    {
        if (_reachedHandshake)
        {
            events.ReportTlsTrust(trust);
        }
        else if (_trustsToHoldBack-- == 0)
        {
            _reachedHandshake = true;
            _heldLines.Clear();
        }
    }

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => PassedOn?.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => PassedOn?.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => PassedOn?.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => PassedOn?.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => PassedOn?.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => PassedOn?.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => PassedOn?.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => PassedOn?.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => PassedOn?.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => PassedOn?.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => PassedOn?.ReportDataReceived(bytes);

    /// <inheritdoc />
    public ValueTask WriteConnectReplyHeadAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken) =>
        PassedOn is IConnectReplyHeadWritingEvents headOutput
            ? headOutput.WriteConnectReplyHeadAsync(head, cancellationToken)
            : ValueTask.CompletedTask;

    // The connect's own events once the redialled handshake is reached, null while what the first
    // attempt reported is repeated.
    private ITransferEvents? PassedOn => _reachedHandshake ? events : null;
}
