using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Tests that <see cref="HttpRequestBodyWriter.SendBytesPerSecond" /> holds a body of bytes to the
/// <c>--limit-rate</c> rate, as curl's <c>CURLOPT_MAX_SEND_SPEED_LARGE</c> does (BL-2957).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterSendRateTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_ThirtyBytesAtTenASecond_SendsTenBytesASecond()
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { SendBytesPerSecond = 10, TimeProvider = clock };
        byte[] body = [.. Enumerable.Range(0, 30).Select(i => (byte)('a' + (i % 26)))];
        Diagnostics.Arrange("body length", body.Length);

        Task writing = writer.WriteAsync(new BytesBody(body, "a/b"), false, CancellationToken.None).AsTask();
        await clock.FirstTimerCreated;
        int afterStart = connection.Written.Length;
        clock.Advance(TimeSpan.FromSeconds(1));
        await clock.TimerCreatedAsync(TimeSpan.FromSeconds(1), 2);
        int afterOneSecond = connection.Written.Length;
        clock.Advance(TimeSpan.FromSeconds(1));
        await writing;

        Diagnostics.Act("written after 0 s, 1 s", (afterStart, afterOneSecond));
        Assert.AreEqual(10, afterStart);
        Assert.AreEqual(20, afterOneSecond);
        CollectionAssert.AreEqual(body, connection.Written);
    }

    [TestMethod]
    public async Task WriteAsync_NoRate_SendsTheBodyWithoutAWait()
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { TimeProvider = clock };

        await writer.WriteAsync(new BytesBody("abc"u8.ToArray(), "a/b"), false, CancellationToken.None);

        Diagnostics.Act("written", connection.Written.Length);
        CollectionAssert.AreEqual("abc"u8.ToArray(), connection.Written);
        Assert.IsFalse(clock.FirstTimerCreated.IsCompleted);
    }
}
