using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Writes TFTP datagrams and transfer results as diagnostic lines (BL-1486): each datagram
/// as a BYTES line whose label names its opcode and block number, so a failed test's log
/// shows the exchange without re-running it.
/// </summary>
internal static class TftpTestDiagnostics
{
    /// <summary>Names a TFTP datagram: its opcode and, for DATA and ACK, its block number.</summary>
    /// <param name="datagram">The datagram.</param>
    /// <returns>For example <c>DATA block 1, 5 payload bytes</c> or <c>ERROR code 1: File not found</c>.</returns>
    public static string Describe(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < 2)
        {
            return "too short for an opcode";
        }

        int opcode = (datagram[0] << 8) | datagram[1];
        int number = datagram.Length >= 4 ? (datagram[2] << 8) | datagram[3] : -1;
        return opcode switch
        {
            1 => "RRQ " + FirstString(datagram[2..]),
            2 => "WRQ " + FirstString(datagram[2..]),
            3 => string.Create(CultureInfo.InvariantCulture, $"DATA block {number}, {Math.Max(0, datagram.Length - 4)} payload bytes"),
            4 => string.Create(CultureInfo.InvariantCulture, $"ACK block {number}"),
            5 => string.Create(CultureInfo.InvariantCulture, $"ERROR code {number}: {(datagram.Length > 4 ? FirstString(datagram[4..]) : string.Empty)}"),
            6 => "OACK " + Encoding.ASCII.GetString(datagram[2..]).Replace('\0', ' ').TrimEnd(),
            _ => string.Create(CultureInfo.InvariantCulture, $"opcode {opcode}"),
        };
    }

    /// <summary>Writes a datagram as a BYTES line labelled with what it is.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What the datagram is, e.g. <c>sent 0</c>.</param>
    /// <param name="datagram">The datagram.</param>
    public static void Datagram(TestDiagnostics diagnostics, string label, ReadOnlySpan<byte> datagram) =>
        diagnostics.Bytes($"{label} [{Describe(datagram)}]", datagram);

    /// <summary>Writes every datagram a scripted channel was sent, with its destination.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="channel">The channel.</param>
    public static void Sent(TestDiagnostics diagnostics, ScriptedDatagramChannel channel)
    {
        diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (int index = 0; index < channel.Sent.Count; index++)
        {
            (byte[] datagram, EndPoint destination) = channel.Sent[index];
            Datagram(diagnostics, string.Create(CultureInfo.InvariantCulture, $"sent {index} to {destination}"), datagram);
        }
    }

    /// <summary>Writes a scripted datagram as an ARRANGE line and a BYTES line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="index">Its place in the script.</param>
    /// <param name="datagram">The datagram.</param>
    /// <param name="source">The endpoint it claims to come from.</param>
    public static void Scripted(TestDiagnostics diagnostics, int index, byte[] datagram, EndPoint source)
    {
        diagnostics.Arrange(string.Create(CultureInfo.InvariantCulture, $"scripted {index} from {source}"), Describe(datagram));
        diagnostics.Bytes(string.Create(CultureInfo.InvariantCulture, $"scripted {index}"), datagram);
    }

    /// <summary>Writes a transfer result's exit code, its error text and the bytes it counted.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void Result(TestDiagnostics diagnostics, TransferResult result)
    {
        diagnostics.Act("exit code", $"{result.ExitCode} ({(int)result.ExitCode})");
        diagnostics.Act("error message", result.ErrorMessage ?? "(none)");
        diagnostics.Act("bytes transferred", result.BytesTransferred);
    }

    private static string FirstString(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }
}
