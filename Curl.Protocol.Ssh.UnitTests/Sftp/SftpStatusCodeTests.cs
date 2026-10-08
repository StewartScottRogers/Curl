using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpStatusCode" /> to what curl 8.21.0 printed on 2026-09-29 for an
/// <c>SSH_FXP_OPEN</c> answered with each status code (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed class SftpStatusCodeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1u, CurlExitCode.Ssh, "Unknown error in libssh2")]
    [DataRow(2u, CurlExitCode.RemoteFileNotFound, "No such file or directory")]
    [DataRow(3u, CurlExitCode.RemoteAccessDenied, "Permission denied")]
    [DataRow(4u, CurlExitCode.Ssh, "Operation failed")]
    [DataRow(5u, CurlExitCode.Ssh, "Bad message from SFTP server")]
    [DataRow(6u, CurlExitCode.Ssh, "Not connected to SFTP server")]
    [DataRow(7u, CurlExitCode.Ssh, "Connection to SFTP server lost")]
    [DataRow(8u, CurlExitCode.Ssh, "Operation not supported by SFTP server")]
    [DataRow(9u, CurlExitCode.Ssh, "Invalid handle")]
    [DataRow(10u, CurlExitCode.RemoteFileNotFound, "No such file or directory")]
    [DataRow(11u, CurlExitCode.RemoteFileExists, "File already exists")]
    [DataRow(12u, CurlExitCode.RemoteAccessDenied, "File is write protected")]
    [DataRow(13u, CurlExitCode.Ssh, "No media")]
    [DataRow(14u, CurlExitCode.RemoteDiskFull, "Disk full")]
    [DataRow(15u, CurlExitCode.RemoteDiskFull, "User quota exceeded")]
    [DataRow(16u, CurlExitCode.Ssh, "Unknown principal")]
    [DataRow(17u, CurlExitCode.RemoteAccessDenied, "File lock conflict")]
    [DataRow(18u, CurlExitCode.QuoteError, "Directory not empty")]
    [DataRow(19u, CurlExitCode.Ssh, "Not a directory")]
    [DataRow(20u, CurlExitCode.Ssh, "Invalid filename")]
    [DataRow(21u, CurlExitCode.Ssh, "Link points to itself")]
    [DataRow(22u, CurlExitCode.Ssh, "Unknown error in libssh2")]
    [DataRow(99u, CurlExitCode.Ssh, "Unknown error in libssh2")]
    public void ExitCodeForAndDescriptionOf_EachCode_MatchWhatCurlPrintedAsMeasured(uint code, CurlExitCode exitCode, string description)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("SSH_FXP_STATUS code", code);

        CurlExitCode actualExitCode = SftpStatusCode.ExitCodeFor(code);
        string actualDescription = SftpStatusCode.DescriptionOf(code);
        diagnostics.Act("exit code", actualExitCode);
        diagnostics.Act("description", actualDescription);

        diagnostics.Assert("exit code", exitCode, actualExitCode);
        diagnostics.Diff("description", description, actualDescription);
        Assert.AreEqual(exitCode, actualExitCode);
        Assert.AreEqual(description, actualDescription);
    }
}
