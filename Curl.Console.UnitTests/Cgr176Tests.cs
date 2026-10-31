using Curl.Engine;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// CGR-176 (BL-3705): curl 8.22.0 draws the progress meter under <c>-s</c> when <c>-T .</c>
/// uploads standard input without blocking; <c>-s -T -</c> stays silent.
/// </summary>
[TestClass]
public sealed class Cgr176Tests
{
    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task RunAsync_SilentUploadOfStandardInputWithDot_DrawsTheProgressMeter()
    {
        string standardError = await RunAsync("-s", "-T", ".", "http://127.0.0.1:18247/a");

        Assert.Contains("% Total", standardError);
    }

    [TestMethod]
    public async Task RunAsync_SilentUploadOfStandardInputWithDash_DrawsNoProgressMeter()
    {
        string standardError = await RunAsync("-s", "-T", "-", "http://127.0.0.1:18247/a");

        Assert.AreEqual(string.Empty, standardError);
    }

    private static async Task<string> RunAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem files = new();
        using MemoryStream standardError = new();
        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                new MemoryStream(),
                standardError,
                new MemoryStream("hello"u8.ToArray()),
                runsOnWindows: false,
                writesProgressMeter: true,
                timeProvider: new ManualTimeProvider())
            .RunAsync(arguments);

        return Encoding.UTF8.GetString(standardError.ToArray());
    }
}
