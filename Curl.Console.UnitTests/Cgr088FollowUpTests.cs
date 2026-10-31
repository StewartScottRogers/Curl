using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Engine;
using Curl.Original.Protocol.File;

namespace Curl.Console;

/// <summary>
/// CGR-088's bad <c>-o</c> path (BL-3951). curl 8.22.0, measured 2026-10-28 with
/// <c>-v -o nodir/x.txt file:///...</c> (12-byte file): <c>Warning: Failed to open the file nodir/x.txt: ...</c>
/// then <c>* client returned ERROR on write of 12 bytes</c>, the meter's newline and
/// <c>* closing connection #0</c>; the handler's <c>Failure writing output to destination</c> line is
/// not curl's for a failed open.
/// </summary>
[TestClass]
public sealed class Cgr088FollowUpTests
{
    private const string BadPath = "nodir/x.txt";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace-ascii")]
    public async Task RunAsync_OutputPathThatCannotBeOpened_EchoesClientReturnedErrorAsAnInfoLine(string option)
    {
        var files = new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("hello world\n") };
        files.UnwritablePaths.Add(BadPath);
        string[] arguments = option == "-v" ? ["-v", "-o", BadPath, "file:///source.txt"] : [option, "%", "-o", BadPath, "file:///source.txt"];

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([new FileProtocolHandler(files)])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);

        string text = Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.Contains("* client returned ERROR on write of 12 bytes\n", text);
        Assert.DoesNotContain("Failure writing output to destination, passed", text);
        Assert.AreEqual(1, text.Split("Warning: Failed to open the file nodir/x.txt").Length - 1);
        Assert.IsLessThan(text.IndexOf("* client returned ERROR", StringComparison.Ordinal), text.IndexOf("Warning: Failed to open the file", StringComparison.Ordinal));
    }
}
