using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins CGR-267 (BL-3796): an <c>sftp://</c> transfer reports the home directory
/// <c>REALPATH .</c> found as <see cref="TransferReport.FtpEntryPath" />, which is what
/// <c>%{ftp_entry_path}</c> prints in curl 8.22.0.
/// </summary>
[TestClass]
public sealed class Cgr267Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    [DataRow("sftp://files.example/data/a", DisplayName = "download")]
    [DataRow("sftp://files.example/data/", DisplayName = "directory listing")]
    public async Task ExecuteAsync_SftpTransfer_ReportsTheHomeDirectoryAsFtpEntryPath(string url)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/a"] = "hello"u8.ToArray();

        TransferResult result = await RunAsync(server, url);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(InMemorySshServer.DefaultHomeDirectory, result.Report?.FtpEntryPath);
    }

    private static async Task<TransferResult> RunAsync(InMemorySshServer server, string url)
    {
        SshProtocolHandler handler = new(
            server,
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
        });
        await server.WhenSessionsEndAsync();
        return result;
    }
}
