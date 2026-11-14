using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>%{filename_effective}</c> names the <c>-o</c> file even when the transfer fails because the
/// URL's scheme is unsupported (exit 1), as curl does (BL-5116).
/// </summary>
[TestClass]
public sealed class FilenameEffectiveOnUnsupportedSchemeTests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UnsupportedScheme_ReportsTheOutputNameAndCreatesNoFile()
    {
        int exitCode = await RunAsync("-s", "-o", "x.out", "-w", "[%{filename_effective}]", "nosuch://x");

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("[x.out]", StandardOutputText);
        Assert.IsEmpty(files.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_UnsupportedSchemeWithJson_CarriesTheOutputName()
    {
        int exitCode = await RunAsync("-s", "-o", "NUL", "-w", "%{json}", "nosuch://x");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("\"filename_effective\":\"NUL\"", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_MalformedUrl_KeepsItsFilenameEffective()
    {
        int exitCode = await RunAsync("-s", "-o", "x.out", "-w", "[%{filename_effective}]", "http://127.0.0.1:99999/");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("[]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferOfASupportedScheme_KeepsItsFilenameEffective()
    {
        int exitCode = await RunAsync("-s", "-o", "x.out", "-w", "[%{filename_effective}]", "http://127.0.0.1:1/");

        Assert.AreEqual(52, exitCode);
        Assert.AreEqual("[x.out]", StandardOutputText);
    }

    private async Task<int> RunAsync(params string[] arguments)
    {
        ScriptedConnector server = new([]);
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
