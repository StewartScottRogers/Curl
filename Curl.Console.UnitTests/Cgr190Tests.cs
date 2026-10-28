using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-190 (BL-3719) in <see cref="CurlCommandRunner" />: <c>-G</c> or <c>--url-query</c> on a URL that does
/// not parse once the query is appended prints <c>curl: (3) Could not parse the URL, failed to set query</c>, sends
/// nothing and leaves <c>%{url_effective}</c> empty. Measured on curl 8.22.0 Schannel (BL-3719 Notes).
/// </summary>
[TestClass]
public sealed class Cgr190Tests
{
    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly InMemoryDataFileReader dataFiles = new();

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    [DataRow("--url-query", "a=b", "http://127.0.0.1:20200/a b")]
    [DataRow("-G", "-d", "a=b", "http://127.0.0.1:99999/a")]
    public async Task RunAsync_QueryAppendedToAUrlThatDoesNotParse_FailsToSetTheQuery(params string[] arguments)
    {
        int exitCode = await RunAsync(["-sS", .. arguments, "-w", "[%{url_effective}]"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) Could not parse the URL, failed to set query" + Environment.NewLine, Text(standardError));
        Assert.AreEqual("[]", Text(standardOutput));
        Assert.IsEmpty(http.Contexts);
    }

    private static string Text(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([http])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            outputPaths: fileSystem,
            configFileReader: dataFiles,
            readEnvironmentVariable: _ => null).RunAsync(arguments);
}
