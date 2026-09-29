using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Writes a TFTP transfer's steps to Curl's diagnostic log under the <c>tftp</c> component
/// (ADR-0222): each packet received at <c>verbose</c>; the request sent, the options the
/// server agreed and the transfer's end at <c>info</c>; a retransmission and a block size
/// the server changed at <c>warning</c>; and an error packet and the failure that ends the
/// transfer at <c>error</c>.
/// </summary>
/// <remarks>
/// Every line tests <see cref="IDiagnosticLog.IsEnabled" /> before it is formatted.
/// </remarks>
/// <param name="diagnosticLog">Where the lines are written.</param>
internal sealed class TftpTransferLog(IDiagnosticLog diagnosticLog)
{
    /// <summary>Writes the request sent and the options it asks for, at <c>info</c>.</summary>
    /// <param name="kind">The request: <c>read</c> or <c>write</c>.</param>
    /// <param name="fileName">The file named in the request.</param>
    /// <param name="blockSize">The <c>blksize</c> asked for, or <see langword="null" /> when no options were sent.</param>
    /// <param name="timeoutSeconds">The <c>timeout</c> asked for.</param>
    public void RequestSent(string kind, string fileName, int? blockSize, int timeoutSeconds)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            string options = blockSize is { } size
                ? $"blksize {size.ToString(CultureInfo.InvariantCulture)}, timeout {timeoutSeconds.ToString(CultureInfo.InvariantCulture)}"
                : "no options";
            Write(DiagnosticLogLevel.Info, $"{kind} request sent for {fileName}: {options}");
        }
    }

    /// <summary>
    /// Writes the options an OACK agreed at <c>info</c>, and at <c>warning</c> a block size
    /// that differs from the one asked for.
    /// </summary>
    /// <param name="options">The OACK's option bytes, after its opcode.</param>
    /// <param name="requestedBlockSize">The <c>blksize</c> asked for, or <see langword="null" />.</param>
    /// <param name="agreedBlockSize">The block size the transfer goes on with.</param>
    public void OptionsAgreed(ReadOnlySpan<byte> options, int? requestedBlockSize, int agreedBlockSize)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, $"options agreed: {DescribeOptions(options)}");
        }

        if (requestedBlockSize != agreedBlockSize && diagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, $"server changed or ignored the blksize asked for: using {agreedBlockSize.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes that the last packet is being sent again, at <c>warning</c>.</summary>
    /// <param name="retry">Which retry this is, from 1.</param>
    /// <param name="retryLimit">How many retries are allowed.</param>
    public void Retransmitting(int retry, int retryLimit)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, $"no answer: sending the last packet again, retry {retry.ToString(CultureInfo.InvariantCulture)} of {retryLimit.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    /// <summary>Writes a received packet's opcode, number and length, at <c>verbose</c>.</summary>
    /// <param name="packet">The packet, at least four bytes long.</param>
    public void Received(ReadOnlySpan<byte> packet)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"received {Describe(TftpPackets.ReadField(packet, 0), TftpPackets.ReadField(packet, 2))}, {packet.Length.ToString(CultureInfo.InvariantCulture)} bytes");
        }
    }

    /// <summary>Writes an error packet's code and text, at <c>error</c>.</summary>
    /// <param name="packet">The error packet, at least four bytes long.</param>
    public void ErrorPacket(ReadOnlySpan<byte> packet)
    {
        if (diagnosticLog.IsEnabled(DiagnosticLogLevel.Error))
        {
            ReadOnlySpan<byte> text = packet[TftpPackets.DataHeaderLength..];
            int end = text.IndexOf((byte)0);
            Write(DiagnosticLogLevel.Error, $"server sent TFTP error {TftpPackets.ReadField(packet, 2).ToString(CultureInfo.InvariantCulture)}: {Encoding.UTF8.GetString(end < 0 ? text : text[..end])}");
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

    private static string Describe(ushort opcode, ushort number) => opcode switch
    {
        TftpPackets.DataOpcode => $"DATA block {number.ToString(CultureInfo.InvariantCulture)}",
        TftpPackets.AcknowledgementOpcode => $"ACK block {number.ToString(CultureInfo.InvariantCulture)}",
        TftpPackets.ErrorOpcode => $"ERROR code {number.ToString(CultureInfo.InvariantCulture)}",
        TftpPackets.OptionAcknowledgementOpcode => "OACK",
        _ => $"opcode {opcode.ToString(CultureInfo.InvariantCulture)}",
    };

    // "blksize\0512\0tsize\011\0" reads as "blksize 512, tsize 11".
    private static string DescribeOptions(ReadOnlySpan<byte> options)
    {
        string[] fields = Encoding.UTF8.GetString(options).TrimEnd('\0').Split('\0');
        var pairs = new List<string>(fields.Length / 2);
        for (int index = 0; index + 1 < fields.Length; index += 2)
        {
            pairs.Add($"{fields[index]} {fields[index + 1]}");
        }

        return string.Join(", ", pairs);
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
        diagnosticLog.Write(level, DiagnosticLogComponents.Tftp, message);
}
