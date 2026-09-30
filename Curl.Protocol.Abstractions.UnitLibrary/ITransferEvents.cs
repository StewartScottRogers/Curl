namespace Curl.Protocol.Abstractions;

/// <summary>
/// Where a protocol handler and its connector report what happens inside a transfer, for
/// <c>-v</c>, <c>--trace</c> and <c>--trace-ascii</c> to render (ADR-0046).
/// </summary>
/// <remarks>
/// The members are libcurl's debug-callback kinds. Every member is synchronous; a byte
/// payload is valid only for the duration of the call, so a sink that keeps it copies it.
/// No member carries a timestamp: <c>--trace-time</c> is stamped by the consumer when the
/// event arrives. <see cref="NoTransferEvents" /> is the sink used when nobody is
/// listening.
/// </remarks>
public interface ITransferEvents
{
    /// <summary>
    /// Reports one of curl's informational lines (<c>CURLINFO_TEXT</c>).
    /// </summary>
    /// <param name="text">The line, without the <c>* </c> prefix and without a line end.</param>
    void ReportInfo(string text);

    /// <summary>
    /// Reports that a new connection was established.
    /// </summary>
    /// <param name="opened">The facts about the connection.</param>
    void ReportConnectionOpened(ConnectionOpenedEvent opened);

    /// <summary>
    /// Reports that an existing connection was reused.
    /// </summary>
    /// <param name="reused">The facts about the connection.</param>
    void ReportConnectionReused(ConnectionReusedEvent reused);

    /// <summary>
    /// Reports that a TLS handshake completed.
    /// </summary>
    /// <param name="handshake">The facts the handshake negotiated.</param>
    void ReportTlsHandshake(TlsHandshakeEvent handshake);

    /// <summary>
    /// Reports raw TLS record bytes (<c>CURLINFO_SSL_DATA_IN</c> and <c>CURLINFO_SSL_DATA_OUT</c>).
    /// </summary>
    /// <param name="bytes">The record bytes.</param>
    /// <param name="sent">
    /// <see langword="true" /> for bytes sent, <see langword="false" /> for bytes received.
    /// </param>
    /// <remarks>
    /// Reserved: <c>SslStream</c> does not expose TLS records, so nothing reports them yet.
    /// <see cref="ReportTlsMessage" /> reports them with what kind of message they are.
    /// </remarks>
    void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent);

    /// <summary>
    /// Reports one TLS protocol message with what kind it is, as OpenSSL's message callback
    /// does (ADR-0085).
    /// </summary>
    /// <param name="message">The message.</param>
    /// <remarks>
    /// By default only its bytes are reported, through <see cref="ReportTlsData" />; a sink
    /// that words the message itself overrides this.
    /// </remarks>
    void ReportTlsMessage(TlsMessageEvent message)
    {
        ReportTlsData(message.Bytes.Span, message.Sent);
    }

    /// <summary>
    /// Reports the trust a TLS connection is set up with, before its handshake (ADR-0085).
    /// </summary>
    /// <param name="trust">The trust.</param>
    /// <remarks>Does nothing by default.</remarks>
    void ReportTlsTrust(TlsTrustEvent trust)
    {
    }

    /// <summary>
    /// Reports the OpenSSL <c>X509_V_</c> code a TLS connection's certificate check ended
    /// with, whether the handshake went on or not: the source of <c>%{ssl_verify_result}</c>
    /// and <c>%{proxy_ssl_verify_result}</c> in the OpenSSL build (BL-661). The Schannel
    /// build reports none, so those print <c>0</c>.
    /// </summary>
    /// <param name="verifyResult">The code, such as <c>0</c> for verified or <c>18</c> for a self-signed certificate.</param>
    /// <param name="isProxy">Whether the connection was to an HTTPS proxy rather than the origin.</param>
    /// <remarks>Does nothing by default.</remarks>
    void ReportCertificateVerifyResult(long verifyResult, bool isProxy)
    {
    }

    /// <summary>
    /// Reports request head bytes exactly as written (<c>CURLINFO_HEADER_OUT</c>), CRLFs
    /// included, one call per write of a head.
    /// </summary>
    /// <param name="bytes">The bytes written.</param>
    void ReportRequestHeader(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reports one received header line (<c>CURLINFO_HEADER_IN</c>), its CRLF included;
    /// the status line and the final blank line are each one call.
    /// </summary>
    /// <param name="bytes">The line's bytes.</param>
    void ReportResponseHeader(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reports body bytes as written to the connection (<c>CURLINFO_DATA_OUT</c>), one call
    /// per write.
    /// </summary>
    /// <param name="bytes">The bytes written.</param>
    void ReportDataSent(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reports body bytes as delivered (<c>CURLINFO_DATA_IN</c>), one call per read.
    /// </summary>
    /// <param name="bytes">The bytes read.</param>
    void ReportDataReceived(ReadOnlySpan<byte> bytes);
}
