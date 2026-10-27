using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-161 (BL-3690) in <see cref="CurlCommandRunner" />: <c>-G -d</c> or <c>--url-query</c> on an
/// <c>ipfs://</c> or <c>ipns://</c> URL fails before any gateway is looked up, as curl appends the query
/// with <c>curl_url_set</c> on the IPFS URL and libcurl's URL API knows no such scheme. Measured on curl
/// 8.21.0 (BL-3690 Notes): <c>curl -sS -G -d a=1 --ipfs-gateway http://127.0.0.1:21802 ipfs://cid/f</c>
/// prints <c>curl: (1) Could not parse the URL, failed to set query</c>, sends nothing and exits 1.
/// </summary>
[TestClass]
public sealed class Cgr161Tests
{
    private const string Url = "ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f";

    private const string WriteOut = "[%{http_code}|%{errormsg}|%{url}|%{url_effective}|%{xfer_id}|%{conn_id}|%{filename_effective}]\\n";

    private static readonly string QueryLine = "curl: (1) Could not parse the URL, failed to set query" + Environment.NewLine;

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly InMemoryDataFileReader dataFiles = new();

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    [DataRow("-G", "-d", "a=1")]
    [DataRow("-G", "-d", "")]
    [DataRow("--url-query", "a=1", "-S")]
    public async Task RunAsync_QueryOnIpfsUrl_PrintsCurlsLineAndReturns1WithoutReachingTheGateway(
        string first,
        string second,
        string third)
    {
        int exitCode = await RunAsync(
            ["-sS", first, second, third, "--ipfs-gateway", "http://127.0.0.1:21802", Url, "-w", WriteOut]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(QueryLine, StandardError);
        Assert.AreEqual("[000|Unsupported protocol|" + Url + "||-1|-1|]\n", StandardOutput);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_QueryOnIpnsUrlWithoutAGateway_IsRefusedBeforeTheGatewayIsLookedUp()
    {
        int exitCode = await RunAsync(["-sS", "--url-query", "a=1", "ipns://k51/p"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(QueryLine, StandardError);
        Assert.IsEmpty(dataFiles.PathsRead);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_QueryOnIpfsUrlUnderSilent_PrintsNothingNamesTheOutputFileAndEndsTheRun()
    {
        int exitCode = await RunAsync(["-s", "-G", "-d", "a=1", "-o", "out1.txt", Url, "http://h/z", "-w", WriteOut]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(string.Empty, StandardError);
        Assert.AreEqual("[000|Unsupported protocol|" + Url + "||-1|-1|out1.txt]\n", StandardOutput);
        Assert.IsEmpty(fileSystem.Written);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_GetWithoutDataOnIpfsUrl_IsRewrittenToTheGateway()
    {
        int exitCode = await RunAsync(["-sS", "-G", "--ipfs-gateway", "http://gw:1", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardError);
        Assert.AreEqual(
            "http://gw:1/ipfs/bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f",
            Assert.ContainsSingle(http.Contexts).Url.OriginalString);
    }

    [TestMethod]
    public async Task RunAsync_QueryOnHttpUrl_IsStillAppended()
    {
        int exitCode = await RunAsync(["-sS", "--url-query", "a=1", "http://h/z"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://h/z?a=1", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
    }

    private string StandardError => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutput => Encoding.ASCII.GetString(standardOutput.ToArray());

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
