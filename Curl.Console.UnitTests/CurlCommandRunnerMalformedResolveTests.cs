using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a malformed <c>--resolve</c> entry exits 49 even when the URL is rejected, as curl 8.22.0 does
/// (AF-0807): it loads the list before it parses the URL.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerMalformedResolveTests
{
    private const string Expected = "curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'";

    [TestMethod]
    [DataRow("/")]
    [DataRow("http://")]
    [DataRow(":1")]
    public async Task RunAsync_MalformedResolveWithRejectedUrl_Exits49(string url)
    {
        (int exitCode, string error) = await RunAsync("-sS", "--resolve", "x:", url);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual(Expected, error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_MalformedResolveWithRejectedQueryUrl_Exits3AsCurlsToolFailsTheQueryFirst()
    {
        (int exitCode, string error) = await RunAsync("-sS", "-G", "--url-query", "a=b", "--resolve", "x:", "http://a b/");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) Could not parse the URL, failed to set query", error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_VerboseMalformedResolveAfterGoodEntry_PrintsAddedLineAndEntryInfoThenExits49()
    {
        (int exitCode, string error) = await RunAsync("-v", "--resolve", "good:80:127.0.0.1", "--resolve", "x:", "/");

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("* [DNS] added good:80:127.0.0.1 to cache\n* Could not parse CURLOPT_RESOLVE entry 'x:'\n" + Expected, error.Replace("\r\n", "\n").TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_WellFormedResolveWithRejectedUrl_StillExits3()
    {
        (int exitCode, _) = await RunAsync("-sS", "--resolve", "x:80:127.0.0.1", "/");

        Assert.AreEqual(3, exitCode);
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(params string[] arguments)
    {
        MemoryStream standardError = new();
        InMemoryFileSystem files = new();
        ScriptedConnector server = new([]);
        HttpProtocolHandler http = new(server, new Curl.Authentication.BasicAndBearerAuthenticator(Curl.Authentication.CredentialEncoding.ForPlatform(isWindows: false)));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), [], loadResolveEntries: events => events.ReportInfo("[DNS] added good:80:127.0.0.1 to cache")),
                files,
                files,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);

        return (exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
