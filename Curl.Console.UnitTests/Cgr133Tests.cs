using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-133 (BL-3662) end to end: <c>curl -sS -o nodir/x.txt -w "[%{exitcode}]\n"</c> of an
/// <c>sftp://</c> or <c>scp://</c> file, where <c>nodir</c> does not exist, exits 23 with
/// <c>curl: (23) client returned ERROR on write of 11 bytes</c> and still prints <c>[23]</c>, and the
/// session ends with <c>DISCONNECT</c> 11, as curl 8.22.0 does; it gave exit 2 and no <c>-w</c> output.
/// The missing directory is under a fresh name in the temporary directory, so nothing is created.
/// </summary>
[TestClass]
public sealed class Cgr133Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task RunAsync_OutputDirectoryMissing_IsExit23AndPrintsWriteOut(string scheme)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();
        string missingDirectory = Path.Combine(Path.GetTempPath(), "cgr133-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(missingDirectory, "x.txt");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sS", "-k", "-u", $"{User}:{Password}", "-o", output, "-w", "[%{exitcode}]\\n", $"{scheme}://127.0.0.1:2222/data/hello.txt"]);
        await server.WhenSessionsEndAsync();

        Assert.AreEqual(23, exitCode);
        Assert.AreEqual("[23]", Encoding.Latin1.GetString(standardOutput.ToArray()).TrimEnd());
        Assert.AreEqual("curl: (23) client returned ERROR on write of 11 bytes", Encoding.UTF8.GetString(standardError.ToArray()).TrimEnd());
        CollectionAssert.Contains(server.Events.ToArray(), "disconnect 11 Shutdown");
        Assert.IsFalse(Directory.Exists(missingDirectory));
    }
}
