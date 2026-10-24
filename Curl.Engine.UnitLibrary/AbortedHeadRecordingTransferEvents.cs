using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps what libcurl still reports after a
/// transfer ends with <c>CURLE_OPERATION_TIMEDOUT</c> because it was too slow: the last response code, the bytes
/// of the response heads, the bytes of the request heads, the body bytes received, the connections opened and
/// the end points of the first. The handler that was
/// cancelled returns no report, so <see cref="ReportOn" /> builds one from these (BL-3321).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class AbortedHeadRecordingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    private const string StatusLinePrefix = "HTTP/";

    private int responseCode;
    private long headerSize;
    private long requestSize;
    private long downloadSize;
    private int connectionCount;
    private IPEndPoint? firstLocalEndPoint;
    private IPEndPoint? firstRemoteEndPoint;

    /// <summary>
    /// The finished transfer, given a report of what was recorded when it ended with
    /// <see cref="CurlExitCode.OperationTimedOut" /> and has none, when anything was recorded; as it is otherwise.
    /// </summary>
    /// <param name="result">The finished transfer, or <see langword="null" /> when a callback aborted it.</param>
    /// <returns>The transfer with the report, or <paramref name="result" /> as it is.</returns>
    internal TransferResult? ReportOn(TransferResult? result) =>
        result is { ExitCode: CurlExitCode.OperationTimedOut, Report: null } && connectionCount + requestSize + headerSize + downloadSize > 0
            ? result with
            {
                Report = new TransferReport
                {
                    ResponseCode = responseCode,
                    HeaderSize = headerSize,
                    RequestSize = requestSize,
                    DownloadSize = downloadSize,
                    ConnectionCount = connectionCount,
                    LocalEndPoint = firstLocalEndPoint,
                    RemoteEndPoint = firstRemoteEndPoint,
                },
            }
            : result;

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        connectionCount++;
        if (connectionCount == 1)
        {
            firstLocalEndPoint = opened.LocalEndPoint;
            firstRemoteEndPoint = opened.RemoteEndPoint;
        }

        inner.ReportConnectionOpened(opened);
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) =>
        inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        requestSize += bytes.Length;
        inner.ReportRequestHeader(bytes);
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        headerSize += bytes.Length;
        if (StatusCodeOf(bytes) is int code)
        {
            responseCode = code;
        }

        inner.ReportResponseHeader(bytes);
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        downloadSize += bytes.Length;
        inner.ReportDataReceived(bytes);
    }

    /// <summary>The status of a status line (<c>HTTP/1.1 200 OK</c>), or <see langword="null" /> for any other header bytes.</summary>
    private static int? StatusCodeOf(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.ASCII.GetString(bytes);
        string[] parts = text.Split(' ', 3);
        return text.StartsWith(StatusLinePrefix, StringComparison.Ordinal) && parts.Length > 1 && int.TryParse(parts[1].AsSpan(0, Math.Min(3, parts[1].Length)), out int code)
            ? code
            : null;
    }
}
