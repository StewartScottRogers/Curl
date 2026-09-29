using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Writes an RTSP transfer's steps to Curl's diagnostic log under the <c>rtsp</c> component
/// (ADR-0222): each reply header's name at <c>verbose</c>; the request's method and
/// <c>CSeq</c>, the reply's status and <c>CSeq</c>, the session ID and the transfer's end at
/// <c>info</c>; a reply head the server closed before its blank line, whose last lines were
/// written unchecked, at <c>warning</c>; and the failure that ends the transfer at <c>error</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted. The URL's
/// user name and password and the <c>Authorization</c> value are never written, and a header
/// is named, never quoted.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class RtspTransferLog(IDiagnosticLog diagnosticLog)
{
    /// <summary>Writes the request sent, by method and <c>CSeq</c>, at <c>info</c>.</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="sequenceNumber">The request's <c>CSeq</c>.</param>
    public void RequestSent(string method, long sequenceNumber)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"{method} request sent with CSeq {sequenceNumber.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes the name of a reply head line's header, at <c>verbose</c>.</summary>
    /// <param name="line">The head line; the status line, which names no header, writes nothing.</param>
    public void HeaderRead(ReadOnlySpan<byte> line)
    {
        int colon = line.IndexOf((byte)':');
        if (colon >= 0 && diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"reply header {Encoding.Latin1.GetString(line[..colon])}");
        }
    }

    /// <summary>
    /// Writes the reply's status, <c>CSeq</c> and session ID at <c>info</c>, and at
    /// <c>warning</c> a head the server closed before its blank line.
    /// </summary>
    /// <param name="head">What was read of the reply head.</param>
    /// <param name="sessionId">The transfer's session ID, or <see langword="null" /> before a reply gave one.</param>
    public void ReplyRead(RtspReplyHead head, string? sessionId)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            string session = sessionId is null ? "no session" : $"session {sessionId}";
            Write(DiagnosticLogLevel.Info, $"reply {head.StatusCode.ToString(CultureInfo.InvariantCulture)} with CSeq {head.SequenceNumber.ToString(CultureInfo.InvariantCulture)}, {session}");
        }

        if (head.EndLine is null && diagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, "server closed before the reply head ended: its last header lines were not read");
        }
    }

    /// <summary>
    /// Writes the transfer's end: its bytes and elapsed milliseconds at <c>info</c> when it
    /// succeeded, otherwise its <see cref="CurlExitCode" /> and message at <c>error</c>.
    /// </summary>
    /// <param name="result">The transfer's outcome.</param>
    /// <param name="elapsed">How long the transfer took.</param>
    public void Ended(TransferResult result, TimeSpan elapsed)
    {
        if (result.IsSuccess)
        {
            Done(result.BytesTransferred, elapsed);
        }
        else
        {
            Failed(result);
        }
    }

    private void Done(long bytes, TimeSpan elapsed)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"transfer done: {bytes.ToString(CultureInfo.InvariantCulture)} bytes in {((long)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)} ms");
        }
    }

    private void Failed(TransferResult result)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, $"transfer failed with {result.ExitCode} (exit {((int)result.ExitCode).ToString(CultureInfo.InvariantCulture)}): {result.ErrorMessage}");
        }
    }

    private void Write(DiagnosticLogLevel level, string message) =>
        diagnosticLog.Write(level, DiagnosticLogComponents.Rtsp, message);
}
