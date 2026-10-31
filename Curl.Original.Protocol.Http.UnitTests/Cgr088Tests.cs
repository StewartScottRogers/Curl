using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-088 (BL-3617) on <see cref="HttpProtocolHandler" />: curl 8.22.0's <c>failf</c>
/// writes a failure's message as a <c>-v</c> line (measured: <c>curl -s -v -f</c> answered 404
/// prints <c>* The requested URL returned error: 404</c>; an empty reply prints <c>* Empty reply
/// from server</c> then <c>* shutting down connection #0</c>).
/// </summary>
[TestClass]
public sealed class Cgr088Tests
{
    [TestMethod]
    public async Task ExecuteAsync_FailOn404_EchoesTheReturnedErrorAsAnInfoLine()
    {
        RecordingTransferEvents events = new();

        TransferResult result = await Execute("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n", events, HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        CollectionAssert.Contains(events.Info, "The requested URL returned error: 404");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyReply_EchoesItThenShutsTheConnectionDown()
    {
        RecordingTransferEvents events = new();

        TransferResult result = await Execute(string.Empty, events, HttpFailMode.None);

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Empty reply from server", "shutting down connection #0" }, events.Info.TakeLast(2).ToArray());
    }

    private static ValueTask<TransferResult> Execute(string response, RecordingTransferEvents events, HttpFailMode fail) =>
        new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:20700/code/404"),
                Output = new MemoryStream(),
                Events = events,
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0", Fail = fail },
            });
}
