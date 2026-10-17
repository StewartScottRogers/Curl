namespace Curl.Engine;

[TestClass]
public sealed class CallbackInterleaveSinkTests
{
    [TestMethod]
    public void Of_NoCallback_ReturnsNull()
    {
        Assert.IsNull(CallbackInterleaveSink.Of(null));
    }

    [TestMethod]
    public async Task WriteAsync_CallbackSet_HandsItTheWholeFrameAndAnswersItsCount()
    {
        byte[]? seen = null;
        var sink = CallbackInterleaveSink.Of(data =>
        {
            seen = data.ToArray();
            return 2;
        })!;
        byte[] frame = [(byte)'$', 0, 0, 1, 9];

        long taken = await sink.WriteAsync(frame, CancellationToken.None);

        Assert.AreEqual(2L, taken);
        CollectionAssert.AreEqual(frame, seen);
    }
}
