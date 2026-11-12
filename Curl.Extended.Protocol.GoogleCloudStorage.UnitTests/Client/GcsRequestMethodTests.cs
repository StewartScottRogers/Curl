using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// Checks that a <c>gs://</c> transfer refuses <c>-I</c> and a <c>-X</c> it cannot send with exit 4 before it connects,
/// and lets through the <c>GET</c> it sends without <c>-T</c> and the <c>PUT</c> it sends with it (ADR-1249).
/// </summary>
[TestClass]
public sealed class GcsRequestMethodTests
{
    private const string Url = "gs://mybucket/obj";

    [TestMethod]
    [DataRow("DELETE", null, "gs:// does not support -X DELETE; it sends only GET, or PUT with -T")]
    [DataRow("PUT", null, "gs:// does not support -X PUT; it sends only GET, or PUT with -T")]
    [DataRow("GET", "body", "gs:// does not support -X GET; it sends only GET, or PUT with -T")]
    public async Task ExecuteAsync_MethodItCannotSend_FailsWithExit4BeforeConnecting(string method, string? upload, string expectedMessage)
    {
        QueuedGcsConnector connector = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context(method, upload, noBody: false));

        Assert.AreEqual((CurlExitCode.NotBuiltIn, expectedMessage), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Head_FailsWithExit4BeforeConnecting()
    {
        QueuedGcsConnector connector = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context(null, null, noBody: true));

        Assert.AreEqual((CurlExitCode.NotBuiltIn, "gs:// does not support -I/--head; it sends only GET, or PUT with -T"), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("GET", null)]
    [DataRow("PUT", "body")]
    public void RefusalOf_MethodItSends_IsNull(string? method, string? upload) =>
        Assert.IsNull(GcsRequestMethod.RefusalOf(Context(method, upload, noBody: false)));

    [TestMethod]
    public void RefusalOf_NoHttpOptions_IsNull() =>
        Assert.IsNull(GcsRequestMethod.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() }));

    private static GcsProtocolHandler Handler(QueuedGcsConnector connector) => new(connector, _ => null);

    private static TransferContext Context(string? method, string? upload, bool noBody) => new()
    {
        Url = CurlUrl.Parse(Url),
        Output = new MemoryStream(),
        Upload = upload is null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(upload)),
        NoBody = noBody,
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { CustomMethod = method },
    };
}
