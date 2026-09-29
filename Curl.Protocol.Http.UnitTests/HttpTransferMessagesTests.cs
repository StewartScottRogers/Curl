using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the HTTP/2 messages of <see cref="HttpTransferMessages" /> that name an error code the
/// way nghttp2's <c>nghttp2_http2_strerror</c> does (BL-658 Notes).
/// </summary>
[TestClass]
public sealed class HttpTransferMessagesTests
{
    [TestMethod]
    [DataRow(Http2ErrorCode.NoError, "NO_ERROR (err 0)", DisplayName = "NO_ERROR")]
    [DataRow(Http2ErrorCode.Http11Required, "HTTP_1_1_REQUIRED (err 13)", DisplayName = "HTTP_1_1_REQUIRED")]
    [DataRow((Http2ErrorCode)0xe, "unknown (err 14)", DisplayName = "unlisted code")]
    public void Http2StreamNotClosedCleanly_ErrorCode_NamesItAsNghttp2Does(Http2ErrorCode errorCode, string ending) =>
        Assert.AreEqual($"HTTP/2 stream 3 was not closed cleanly: {ending}", HttpTransferMessages.Http2StreamNotClosedCleanly(3, errorCode));

    [TestMethod]
    public void Http2ShutsDownConnection_ErrorCode_NamesItAsNghttp2Does() =>
        Assert.AreEqual("nghttp2 shuts down connection with error 3: FLOW_CONTROL_ERROR", HttpTransferMessages.Http2ShutsDownConnection(Http2ErrorCode.FlowControlError));
}
