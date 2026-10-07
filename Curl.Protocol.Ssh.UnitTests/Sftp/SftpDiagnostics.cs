using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Writes what an SFTP test arranges and gets - the scripted server's bytes, the SFTP
/// requests the client wrote with their decoded packet type and request id, a transfer's
/// result and an <see cref="SshTransferException" /> - as <c>ARRANGE</c>, <c>ACT</c> and
/// <c>BYTES</c> lines through the shared <see cref="TestDiagnostics" /> helper (BL-1628).
/// </summary>
internal static class SftpDiagnostics
{
    /// <summary>Writes the scripted server's bytes as an ARRANGE line with their length, then the bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="script">What the in-memory server sends.</param>
    public static void ArrangeScript(this TestDiagnostics diagnostics, SftpServerScript script)
    {
        diagnostics.Arrange("server script", $"{script.Bytes.Length} bytes");
        diagnostics.Bytes("server script", script.Bytes);
    }

    /// <summary>
    /// Writes the SFTP requests the client wrote as an ACT line naming each one's packet type
    /// and request id, then each request's bytes.
    /// </summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="written">The bytes the client wrote to the connection.</param>
    public static void ActRequests(this TestDiagnostics diagnostics, byte[] written)
    {
        List<byte[]> requests = SftpServerScript.SftpRequests(written);
        diagnostics.Act("SFTP requests", $"{requests.Count}: {string.Join(", ", requests.Select(DescribeRequest))}");
        for (int index = 0; index < requests.Count; index++)
        {
            diagnostics.Bytes($"SFTP request {index}", requests[index]);
        }
    }

    /// <summary>Writes a transfer's result as an ACT line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The transfer's result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result) =>
        diagnostics.Act("transfer result", result);

    /// <summary>Writes a failure's exit code, message and verbose-line flag as an ACT line.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="failure">The failure.</param>
    public static void ActFailure(this TestDiagnostics diagnostics, SshTransferException failure) =>
        diagnostics.Act("failure", $"exit {failure.ExitCode} ({(int)failure.ExitCode}): {failure.Message} (verbose line {failure.IsVerboseLine})");

    /// <summary>Describes one SFTP request by its packet type and, past <c>SSH_FXP_INIT</c>, its request id.</summary>
    /// <param name="request">The request, packet type first, without its length.</param>
    /// <returns>The description.</returns>
    private static string DescribeRequest(byte[] request) =>
        request.Length < 5 || request[0] == SftpPacketType.Init
            ? $"type {request[0]}"
            : $"type {request[0]} id {System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(request.AsSpan(1))}";
}
