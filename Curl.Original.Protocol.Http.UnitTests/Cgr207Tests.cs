using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins CGR-207: a <c>Set-Cookie</c> header just before a header line without a colon is whole,
/// so curl 8.22.0 stores its cookie before it fails with <c>Header without colon</c> (exit 8).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_SetCookieJustBeforeAHeaderWithoutColon_StoresBothCookiesAndFails()
    {
        const string response = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nSet-Cookie: d=2\r\nbogus line\r\nContent-Length: 2\r\n\r\nok";
        ScriptedCookieStore store = new();
        Diagnostics.Arrange("response", response);

        TransferResult result = await CookieHandler(QueueConnector.For(Connection(response, 65536)), store)
            .ExecuteAsync(CookieContext(CookieUrl));

        WriteResult(result);
        string[] stored = [.. store.Responses.Select(stored => stored.SetCookieHeader)];
        Diagnostics.Act("exit code, error, stored", $"{result.ExitCode}, {result.ErrorMessage}, {string.Join(" | ", stored)}");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Header without colon", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "a=1", "d=2" }, stored);
    }
}
