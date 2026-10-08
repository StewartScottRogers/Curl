using Curl.Cli;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--unix-socket</c> and <c>--abstract-unix-socket</c> reach
/// <see cref="HttpRequestOptions.OverUnixSocket" /> through
/// <see cref="HttpRequestOptionsMapping.FromCommandLine" />, so the HTTP handler can refuse
/// HTTP/3 over a Unix domain socket before connecting, as curl 8.21.0 does (BL-867).
/// </summary>
[TestClass]
public sealed class HttpUnixSocketMappingTests
{
    private const string Url = "https://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--unix-socket")]
    [DataRow("--abstract-unix-socket")]
    public void FromCommandLine_UnixSocket_MarksTheTransferOverAUnixSocket(string option)
    {
        Diagnostics.Arrange("arguments", $"{option} /tmp/curl.sock {Url}");
        CommandLineParseResult result = CommandLineParser.Parse([option, "/tmp/curl.sock", Url]);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);

        bool overUnixSocket = HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket;
        Diagnostics.Act("over unix socket", overUnixSocket);

        Diagnostics.Assert("over unix socket", true, overUnixSocket);
        Assert.IsTrue(HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket);
    }

    [TestMethod]
    public void FromCommandLine_NoUnixSocket_LeavesTheTransferOffAUnixSocket()
    {
        Diagnostics.Arrange("arguments", Url);
        CommandLineParseResult result = CommandLineParser.Parse([Url]);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);

        bool overUnixSocket = HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket;
        Diagnostics.Act("over unix socket", overUnixSocket);

        Diagnostics.Assert("over unix socket", false, overUnixSocket);
        Assert.IsFalse(HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket);
    }
}
