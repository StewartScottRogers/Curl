using System.Net;
using System.Text;
using Curl.Original.Protocol.Ssh.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins BL-5280: after an SFTP download the port sends <c>SSH_MSG_DISCONNECT</c> while the SFTP
/// channel is still open, as curl 8.21.0 with libssh2 does, so the server sees no channel close
/// before the disconnect.
/// </summary>
[TestClass]
public sealed class SftpDisconnectOrderTests
{
    [TestMethod]
    public async Task ExecuteAsync_SftpDownload_SendsDisconnectBeforeAnyChannelClose()
    {
        InMemorySshServer server = new("tester", "secret");
        server.Files["/a.txt"] = "hello world!"u8.ToArray();
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
            Url = CurlUrl.Parse("sftp://files.example/a.txt"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("tester", "secret"),
        });
        await server.WhenSessionsEndAsync();

        Assert.IsTrue(result.IsSuccess);
        string[] events = [.. server.Events];
        int disconnect = Array.IndexOf(events, "disconnect 11 Shutdown");
        Assert.IsGreaterThanOrEqualTo(0, disconnect);
        CollectionAssert.DoesNotContain(events[..disconnect], "channel close");
    }
}
