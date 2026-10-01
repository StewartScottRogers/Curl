using Curl.Cli;
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

    [TestMethod]
    [DataRow("--unix-socket")]
    [DataRow("--abstract-unix-socket")]
    public void FromCommandLine_UnixSocket_MarksTheTransferOverAUnixSocket(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "/tmp/curl.sock", Url]);
        Assert.IsTrue(result.IsAccepted);

        Assert.IsTrue(HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket);
    }

    [TestMethod]
    public void FromCommandLine_NoUnixSocket_LeavesTheTransferOffAUnixSocket()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);
        Assert.IsTrue(result.IsAccepted);

        Assert.IsFalse(HttpRequestOptionsMapping.FromCommandLine(result.Options).OverUnixSocket);
    }
}
