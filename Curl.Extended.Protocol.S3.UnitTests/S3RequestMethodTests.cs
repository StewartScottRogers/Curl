using Curl.Extended.Protocol.S3.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3;

/// <summary>
/// Checks that an <c>s3://</c> transfer refuses <c>-I</c> and a <c>-X</c> it cannot send with exit 4 before it connects,
/// and lets through the <c>GET</c> it sends without <c>-T</c> and the <c>PUT</c> it sends with it (ADR-1238).
/// </summary>
[TestClass]
public sealed class S3RequestMethodTests
{
    private const string Url = "s3://mybucket/obj";

    [TestMethod]
    [DataRow("DELETE", null, "s3:// does not support -X DELETE; it sends only GET, or PUT with -T")]
    [DataRow("PUT", null, "s3:// does not support -X PUT; it sends only GET, or PUT with -T")]
    [DataRow("GET", "body", "s3:// does not support -X GET; it sends only GET, or PUT with -T")]
    public async Task ExecuteAsync_MethodItCannotSend_FailsWithExit4BeforeConnecting(string method, string? upload, string expectedMessage)
    {
        QueuedS3Connector connector = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context(method, upload, noBody: false));

        Assert.AreEqual((CurlExitCode.NotBuiltIn, expectedMessage), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_Head_FailsWithExit4BeforeConnecting()
    {
        QueuedS3Connector connector = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context(null, null, noBody: true));

        Assert.AreEqual((CurlExitCode.NotBuiltIn, "s3:// does not support -I/--head; it sends only GET, or PUT with -T"), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("GET", null)]
    [DataRow("PUT", "body")]
    public void RefusalOf_MethodItSends_IsNull(string? method, string? upload) =>
        Assert.IsNull(S3RequestMethod.RefusalOf(Context(method, upload, noBody: false)));

    [TestMethod]
    public void RefusalOf_NoHttpOptions_IsNull() =>
        Assert.IsNull(S3RequestMethod.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream() }));

    private static S3ProtocolHandler Handler(QueuedS3Connector connector) => new(connector, _ => null, 8);

    private static TransferContext Context(string? method, string? upload, bool noBody) => new()
    {
        Url = CurlUrl.Parse(Url),
        Output = new MemoryStream(),
        Upload = upload is null ? null : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(upload)),
        NoBody = noBody,
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { CustomMethod = method },
    };
}
