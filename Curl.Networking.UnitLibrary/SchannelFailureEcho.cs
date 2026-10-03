using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Reports, as a <c>-v</c> information line, the failure text curl's Schannel build writes with
/// <c>failf</c> before it closes the connection: <c>failf</c> keeps the text for the
/// <c>curl: (NN)</c> line and also writes it under <c>-v</c>, so <c>-v</c> shows it after the
/// ALPN offer and before <c>closing connection #0</c> (curl 8.21.0, measured 2026-10-03, BL-1323).
/// </summary>
/// <remarks>
/// Only the failures whose echo has been measured are reported: the untrusted root's
/// <c>SEC_E_UNTRUSTED_ROOT</c> of exit 60. The OpenSSL build's lines before a failure differ
/// and are not reported here.
/// </remarks>
internal static class SchannelFailureEcho
{
    /// <summary>
    /// Reports the failure's text as an information line when the provider behaves as the
    /// Schannel build and the text is one this class echoes, and returns the failure unchanged.
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="matchesSchannelBuild">Whether the provider behaves as curl's Schannel build.</param>
    /// <param name="failure">The failed handshake's result.</param>
    /// <returns><paramref name="failure" />.</returns>
    internal static ConnectResult Report(ITransferEvents events, bool matchesSchannelBuild, ConnectResult failure)
    {
        if (matchesSchannelBuild && failure.ErrorMessage == TlsFailureMessages.SchannelUntrustedRoot)
        {
            events.ReportInfo(failure.ErrorMessage);
        }

        return failure;
    }
}
