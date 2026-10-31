using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-281 (BL-3810): <c>--limit-rate</c> shrinks the receive window
/// curl grows an HTTP/2 stream to; 400k gives WINDOW_UPDATE 139265 (204800 - 65535), twice.
/// </summary>
[TestClass]
public sealed class Cgr281Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_WithReceiveRate400k_SendsWindowUpdateIncrement139265Twice()
    {
        ScriptedConnection connection = new([], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        stream.ReceiveBytesPerSecond = 400 * 1024;
        Diagnostics.Arrange("receive rate", "400k");

        await stream.WriteAsync(System.Text.Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        string written = Convert.ToHexString(connection.Written);
        const string Update = "00000408000000000100022001";
        Diagnostics.Act("bytes written (hex)", written);
        Assert.AreEqual(2, written.Split(Update).Length - 1);
    }

    [TestMethod]
    public async Task WriteAsync_WithReceiveRateBelowTheInitialWindow_SendsNoWindowUpdate()
    {
        ScriptedConnection connection = new([], 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        stream.ReceiveBytesPerSecond = 1000;

        await stream.WriteAsync(System.Text.Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        Assert.IsFalse(Convert.ToHexString(connection.Written).Contains("000004080000000001", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StreamReceiveWindowSizeFor_HugeRate_IsCappedAtTenMebibytes() =>
        Assert.AreEqual(Http2Session.StreamReceiveWindowSize, Http2Session.StreamReceiveWindowSizeFor(long.MaxValue));
}
