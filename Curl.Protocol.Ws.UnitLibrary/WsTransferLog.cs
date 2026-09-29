using System.Buffers.Binary;
using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Writes a WebSocket transfer's steps to Curl's diagnostic log under the <c>ws</c> component
/// (ADR-0222): each frame received and the upload frame sent at <c>verbose</c>; the upgrade
/// request, the upgrade accepted and the transfer's end at <c>info</c>; a close frame with an
/// unexpected code at <c>warning</c>; and the failure that ends the transfer at <c>error</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted. The URL's
/// user name and password and the <c>Authorization</c> value are never written: the request
/// is named by its method and request target only.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class WsTransferLog(IDiagnosticLog diagnosticLog)
{
    /// <summary>The close code of a normal closure.</summary>
    private const int NormalClosure = 1000;

    /// <summary>The close code of an endpoint going away.</summary>
    private const int GoingAway = 1001;

    /// <summary>Writes the upgrade request sent, at <c>info</c>.</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="requestTarget">The request's target, without the URL's credentials.</param>
    public void UpgradeRequested(string method, string requestTarget)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"upgrade request sent: {method} {requestTarget}");
        }
    }

    /// <summary>Writes that the server accepted the upgrade, at <c>info</c>.</summary>
    public void UpgradeAccepted()
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "upgrade accepted: 101, switched to WebSocket");
        }
    }

    /// <summary>Writes a frame's opcode, FIN bit and payload length, at <c>verbose</c>.</summary>
    /// <param name="direction"><c>received</c> or <c>sent</c>.</param>
    /// <param name="opcode">The frame's opcode.</param>
    /// <param name="isFinal">Whether the frame's FIN bit is set.</param>
    /// <param name="payloadLength">The frame's payload length.</param>
    public void Frame(string direction, WsOpcode opcode, bool isFinal, long payloadLength)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            string fin = isFinal ? "FIN" : "no FIN";
            Write(DiagnosticLogLevel.Verbose, $"{direction} {opcode.ToString().ToUpperInvariant()} frame, {fin}, {payloadLength.ToString(CultureInfo.InvariantCulture)} bytes");
        }
    }

    /// <summary>
    /// Writes a close frame's code at <c>warning</c> when it is neither 1000 (normal closure)
    /// nor 1001 (going away); a close frame without a code is not a warning.
    /// </summary>
    /// <param name="payload">The close frame's payload.</param>
    public void CloseReceived(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(ushort) || !diagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
        {
            return;
        }

        int code = BinaryPrimitives.ReadUInt16BigEndian(payload);
        if (code is not (NormalClosure or GoingAway))
        {
            Write(DiagnosticLogLevel.Warning, $"server closed with unexpected code {code.ToString(CultureInfo.InvariantCulture)}");
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
        diagnosticLog.Write(level, DiagnosticLogComponents.Ws, message);
}
