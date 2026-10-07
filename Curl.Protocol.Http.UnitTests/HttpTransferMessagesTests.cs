using Curl.Http2;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the HTTP/2 messages of <see cref="HttpTransferMessages" /> that name an error code the
/// way nghttp2's <c>nghttp2_http2_strerror</c> does (BL-658 Notes).
/// </summary>
[TestClass]
public sealed class HttpTransferMessagesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(Http2ErrorCode.NoError, "NO_ERROR (err 0)", DisplayName = "NO_ERROR")]
    [DataRow(Http2ErrorCode.Http11Required, "HTTP_1_1_REQUIRED (err 13)", DisplayName = "HTTP_1_1_REQUIRED")]
    [DataRow((Http2ErrorCode)0xe, "unknown (err 14)", DisplayName = "unlisted code")]
    public void Http2StreamNotClosedCleanly_ErrorCode_NamesItAsNghttp2Does(Http2ErrorCode errorCode, string ending)
    {
        Diagnostics.Arrange("error code", (int)errorCode);

        string message = HttpTransferMessages.Http2StreamNotClosedCleanly(3, errorCode);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", $"HTTP/2 stream 3 was not closed cleanly: {ending}", message);
        Assert.AreEqual($"HTTP/2 stream 3 was not closed cleanly: {ending}", message);
    }

    [TestMethod]
    public void Http2ShutsDownConnection_ErrorCode_NamesItAsNghttp2Does()
    {
        Diagnostics.Arrange("error code", (int)Http2ErrorCode.FlowControlError);

        string message = HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.FlowControlError);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "nghttp2 shuts down connection with error 3: FLOW_CONTROL_ERROR", message);
        Assert.AreEqual("nghttp2 shuts down connection with error 3: FLOW_CONTROL_ERROR", message);
    }
}
