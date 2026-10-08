using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Writes what an SCP or SFTP transfer test arranges and gets - the URL path and the scripted
/// server's bytes, the transfer's result, the bytes written and the progress reported, and the
/// client's SSH messages and SFTP requests by name and bytes - as <c>ARRANGE</c>, <c>ACT</c>,
/// <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c> lines through the shared
/// <see cref="TestDiagnostics" /> helper (BL-1627).
/// </summary>
internal static class SshFileTransferDiagnostics
{
    /// <summary>The most requests or messages named, or written as <c>BYTES</c> lines; the rest are counted.</summary>
    private const int RequestCap = 12;

    /// <summary>Writes the transfer's URL path and the scripted server's bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="urlPath">The URL path transferred.</param>
    /// <param name="serverScript">The bytes the scripted server sends.</param>
    public static void ArrangeTransfer(this TestDiagnostics diagnostics, string urlPath, byte[] serverScript)
    {
        diagnostics.Arrange("url path", urlPath);
        diagnostics.Arrange("server script", string.Create(CultureInfo.InvariantCulture, $"{serverScript.Length} bytes"));
        diagnostics.Bytes("server script", serverScript);
    }

    /// <summary>Writes a transfer's result, the bytes it wrote and the progress it reported.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result, or <see langword="null" /> when the transfer threw.</param>
    /// <param name="output">The bytes written.</param>
    /// <param name="progress">The progress reports, bytes so far and the expected total.</param>
    public static void ActTransfer(this TestDiagnostics diagnostics, TransferResult? result, byte[] output, IReadOnlyList<(long, long?)> progress)
    {
        diagnostics.Act("result", result?.ToString() ?? "(none)");
        diagnostics.ActBytes("output", output);
        diagnostics.Act("progress", Progress(progress));
    }

    /// <summary>Writes the SSH messages the client sent, by name, then the first ones' bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="written">The bytes the client wrote to the connection.</param>
    public static void ActSshMessages(this TestDiagnostics diagnostics, byte[] written) =>
        ActList(diagnostics, "client SSH messages", SftpServerScript.SshPayloads(written), SshAuthenticationDiagnostics.MessageName);

    /// <summary>Writes the SFTP requests the client sent, by name, then the first ones' bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="written">The bytes the client wrote to the connection.</param>
    public static void ActSftpRequests(this TestDiagnostics diagnostics, byte[] written) =>
        ActList(diagnostics, "SFTP requests", SftpServerScript.SftpRequests(written), RequestName);

    /// <summary>Writes the ASSERT line between the expected and the actual result.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected result.</param>
    /// <param name="actual">The actual result.</param>
    public static void AssertResult(this TestDiagnostics diagnostics, TransferResult? expected, TransferResult? actual) =>
        diagnostics.Assert("result", expected?.ToString() ?? "(none)", actual?.ToString() ?? "(none)");

    /// <summary>Writes the ASSERT line between two texts, each escaped, then their first difference.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="actual">The actual text.</param>
    public static void AssertText(this TestDiagnostics diagnostics, string label, string expected, string actual)
    {
        diagnostics.Assert(label, SshAuthenticationDiagnostics.Text(expected), SshAuthenticationDiagnostics.Text(actual));
        diagnostics.Diff(label, expected, actual);
    }

    /// <summary>Writes the ASSERT line between expected and actual progress reports.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected reports.</param>
    /// <param name="actual">The actual reports.</param>
    public static void AssertProgress(this TestDiagnostics diagnostics, IReadOnlyList<(long, long?)> expected, IReadOnlyList<(long, long?)> actual) =>
        diagnostics.Assert("progress", Progress(expected), Progress(actual));

    /// <summary>Writes the ASSERT line for a request count, then the first difference of each expected request.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="requests">The requests sent.</param>
    /// <param name="skipped">How many requests come before the expected ones.</param>
    /// <param name="expected">The expected requests.</param>
    public static void DiffRequests(this TestDiagnostics diagnostics, IReadOnlyList<byte[]> requests, int skipped, IReadOnlyList<byte[]> expected)
    {
        diagnostics.Assert("request count", skipped + expected.Count, requests.Count);
        for (int index = 0; index < Math.Min(expected.Count, requests.Count - skipped); index++)
        {
            diagnostics.Diff($"request {skipped + index} ({RequestName(expected[index])})", expected[index], requests[skipped + index]);
        }
    }

    /// <summary>Formats progress reports as one list.</summary>
    /// <param name="progress">The reports.</param>
    /// <returns>For example <c>1 reports [5/10]</c>, with <c>?</c> for an unknown total.</returns>
    public static string Progress(IReadOnlyList<(long, long?)> progress) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{progress.Count} reports [{string.Join(", ", progress.Take(RequestCap).Select(report => string.Create(CultureInfo.InvariantCulture, $"{report.Item1}/{(report.Item2 is long total ? total.ToString(CultureInfo.InvariantCulture) : "?")}")))}{(progress.Count > RequestCap ? ", ..." : string.Empty)}]");

    /// <summary>Names an SFTP packet by its type, as draft-ietf-secsh-filexfer-02 does.</summary>
    /// <param name="packet">The packet, type first.</param>
    /// <returns>For example <c>5 SSH_FXP_READ</c>, or <c>(empty)</c>.</returns>
    public static string RequestName(byte[] packet) =>
        packet.Length == 0 ? "(empty)" : string.Create(CultureInfo.InvariantCulture, $"{packet[0]} {PacketTypeName(packet[0])}");

    private static void ActList(TestDiagnostics diagnostics, string label, List<byte[]> items, Func<byte[], string> name)
    {
        string names = string.Join(", ", items.Take(RequestCap).Select(name));
        diagnostics.Act(label, string.Create(CultureInfo.InvariantCulture, $"{items.Count} [{names}{(items.Count > RequestCap ? ", ..." : string.Empty)}]"));
        for (int index = 0; index < Math.Min(items.Count, RequestCap); index++)
        {
            diagnostics.Bytes($"{label} {index}", items[index]);
        }
    }

    private static string PacketTypeName(byte type) => type switch
    {
        SftpPacketType.Init => "SSH_FXP_INIT",
        SftpPacketType.Version => "SSH_FXP_VERSION",
        SftpPacketType.Open => "SSH_FXP_OPEN",
        SftpPacketType.Close => "SSH_FXP_CLOSE",
        SftpPacketType.Read => "SSH_FXP_READ",
        SftpPacketType.Write => "SSH_FXP_WRITE",
        SftpPacketType.SetStat => "SSH_FXP_SETSTAT",
        SftpPacketType.OpenDirectory => "SSH_FXP_OPENDIR",
        SftpPacketType.ReadDirectory => "SSH_FXP_READDIR",
        SftpPacketType.Remove => "SSH_FXP_REMOVE",
        SftpPacketType.MakeDirectory => "SSH_FXP_MKDIR",
        SftpPacketType.RemoveDirectory => "SSH_FXP_RMDIR",
        SftpPacketType.RealPath => "SSH_FXP_REALPATH",
        SftpPacketType.Stat => "SSH_FXP_STAT",
        SftpPacketType.Rename => "SSH_FXP_RENAME",
        SftpPacketType.ReadLink => "SSH_FXP_READLINK",
        SftpPacketType.SymbolicLink => "SSH_FXP_SYMLINK",
        SftpPacketType.Status => "SSH_FXP_STATUS",
        SftpPacketType.Handle => "SSH_FXP_HANDLE",
        SftpPacketType.Data => "SSH_FXP_DATA",
        SftpPacketType.Name => "SSH_FXP_NAME",
        SftpPacketType.Attributes => "SSH_FXP_ATTRS",
        SftpPacketType.Extended => "SSH_FXP_EXTENDED",
        SftpPacketType.ExtendedReply => "SSH_FXP_EXTENDED_REPLY",
        _ => "unknown",
    };
}
