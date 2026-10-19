using System.Text;
using System.Text.RegularExpressions;
using Curl.Api.Codes;
using Curl.Api.Options.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// Pins the Digest state an easy handle keeps from one perform to the next, through the managed engine and its real
/// HTTP handler over a scripted connection (upstream case 2027, BL-2745).
/// </summary>
[TestClass]
public sealed class CurlEasyKeptDigestTests
{
    private static string Refused(int nonce) =>
        $"HTTP/1.1 401 Wrong\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"{nonce}\"\r\nContent-Length: 0\r\n\r\n";

    /// <summary>
    /// lib2023's second round: a perform whose Digest answer is refused keeps the refusing 401's nonce, so the next
    /// perform answers it up front and, as libcurl ignores that perform's first 401 Digest header as a duplicate,
    /// sends the same answer once more before the next 401 ends it. The pattern takes both builds' separators: the
    /// Schannel build's SSPI writes <c>nonce="5",uri=</c>, the OpenSSL build's own Digest code <c>nonce="5", uri=</c>.
    /// </summary>
    [TestMethod]
    public async Task Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform()
    {
        ScriptedConnector connector = new([.. new[] { Refused(4), Refused(5), Refused(7), Refused(7) }.Select(Encoding.ASCII.GetBytes)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Userpwd, "testuser:wrongpass");
        easy.SetOption(CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest);
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/first");
        easy.Perform();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/second");

        CurlCode code = easy.Perform();

        string written = Encoding.ASCII.GetString(connector.Written);
        string second = written[written.IndexOf("GET /second", StringComparison.Ordinal)..];
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(2, second.Split("GET /second HTTP/1.1\r\n").Length - 1);
        Assert.AreEqual(2, Regex.Count(second, "nonce=\"5\", ?uri=\"/second\""));
        Assert.DoesNotContain("nonce=\"7\"", second);
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
