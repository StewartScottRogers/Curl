using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-4932 on <see cref="HttpProtocolHandler" />: a body sent after <c>100 Continue</c> shows
/// the <c>-v</c> lines of that 1xx head before its own data lines, as curl 8.22.0 does (measured,
/// BL-4932 Notes).
/// </summary>
[TestClass]
public sealed class UploadVerboseDataLinesTests
{
    private const string Response = "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_BodyAfter100Continue_ReportsThe100HeadBeforeTheFirstDataLine()
    {
        RecordingTransferEvents events = new();
        GatedConnection connection = new(Encoding.Latin1.GetBytes(Response), 65536, 0);
        Diagnostics.Arrange("response", Response.Replace("\r\n", "\\r\\n"));

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21999/a"),
                Output = new MemoryStream(),
                Http = new HttpRequestOptions { Body = new BytesBody(new byte[2_000_000], "application/octet-stream") },
                Events = events,
            });

        int continueHead = events.Events.FindIndex(line => line.StartsWith("< HTTP/1.1 100 Continue", StringComparison.Ordinal));
        int firstData = events.Events.FindIndex(line => line.StartsWith("} ", StringComparison.Ordinal));
        Diagnostics.Act("exit code, 100 head at, first data at", $"{result.ExitCode}, {continueHead}, {firstData}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsGreaterThanOrEqualTo(0, continueHead);
        Assert.IsLessThan(firstData, continueHead);
        Assert.AreEqual(1, events.Events.Count(line => line.StartsWith("< HTTP/1.1 100 Continue", StringComparison.Ordinal)));
    }
}
