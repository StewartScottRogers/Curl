using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins which closing <c>-v</c> line follows an "Error in the SSH layer" failure: an <c>sftp</c>
/// directory listing reset at its READDIR leaves the connection intact, because curl reads it before the transfer
/// phase, (AF-0752).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_SftpListingResetAtReadDirectory_LeavesTheConnectionIntact()
    {
        InMemorySshServer server = new(User, Password) { ResetsAt = "sftp 12 /data/" };
        server.Files["/data/f"] = Hello;

        string lines = await RunRecordingLinesAsync(server, "sftp://files.example/data/");

        Diagnostics.Assert("closing line", "* Connection #0 to host files.example:22 left intact", lines.Split(" | ")[^1]);
        Assert.EndsWith("* Connection #0 to host files.example:22 left intact", lines);
    }
}
