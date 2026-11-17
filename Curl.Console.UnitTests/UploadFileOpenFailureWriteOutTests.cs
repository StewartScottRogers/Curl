using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that when <c>-T</c> cannot open its file, <c>-w</c> reports <c>%{filename_effective}</c> as the <c>-o</c> name
/// and <c>%{scheme}</c> as nothing, as curl does, with exit 26 (BL-5339).
/// </summary>
[TestClass]
public sealed class UploadFileOpenFailureWriteOutTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_MissingUploadFile_ReportsOutputNameAndNoScheme()
    {
        files.UnreadablePaths.Add("nofile.txt");
        int exitCode = await RunAsync("-sS", "-T", "nofile.txt", "-o", "out.txt", "-w", "[%{filename_effective}][%{scheme}][%{exitcode}]\n", "http://127.0.0.1:30080/echo/");

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("[out.txt][][26]\n", StandardOutputText);
        Assert.Contains("curl: cannot open 'nofile.txt'", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_MissingUploadFileWithJson_CarriesNameAndNullScheme()
    {
        files.UnreadablePaths.Add("nofile.txt");
        int exitCode = await RunAsync("-sS", "-T", "nofile.txt", "-o", "NUL", "-w", "%{json}", "http://127.0.0.1:30080/echo/");

        Assert.AreEqual(26, exitCode);
        Assert.Contains("\"filename_effective\":\"NUL\"", StandardOutputText);
        Assert.Contains("\"scheme\":null", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_MissingUploadFileInParallel_ReportsTheSame()
    {
        files.UnreadablePaths.Add("nofile.txt");
        int exitCode = await RunAsync("-sS", "-Z", "-T", "nofile.txt", "-o", "out.txt", "-w", "[%{filename_effective}][%{scheme}][%{exitcode}]\n", "http://127.0.0.1:30080/echo/");

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("[out.txt][][26]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SuccessfulUpload_StillReportsHttpScheme()
    {
        files.ExistingContent["up.txt"] = "data"u8.ToArray();

        int exitCode = await RunAsync("-sS", "-T", "up.txt", "-o", "out.txt", "-w", "[%{scheme}]", "http://127.0.0.1:30080/echo/");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("[http]", StandardOutputText);
    }

    private async Task<int> RunAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        return await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
    }
}
