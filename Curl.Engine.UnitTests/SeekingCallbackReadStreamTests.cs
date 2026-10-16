using Curl.Api.Callbacks;

namespace Curl.Engine;

[TestClass]
public sealed class SeekingCallbackReadStreamTests
{
    [TestMethod]
    public void RewindToStart_SeekReturnsOk_SeeksToTheStartAndForgetsWhatWasRead()
    {
        List<(long Offset, SeekOrigin Origin)> seeks = [];
        using SeekingCallbackReadStream stream = new(buffer => buffer.Length, (offset, origin) =>
        {
            seeks.Add((offset, origin));
            return SeekResult.Ok;
        });
        bool unread = stream.HasBeenRead;
        _ = stream.Read(new byte[2], 0, 2);
        bool read = stream.HasBeenRead;

        string? failure = stream.RewindToStart();

        Assert.IsFalse(unread);
        Assert.IsTrue(read);
        Assert.IsNull(failure);
        Assert.IsFalse(stream.HasBeenRead);
        CollectionAssert.AreEqual(new[] { (0L, SeekOrigin.Begin) }, seeks);
    }

    [TestMethod]
    [DataRow(SeekResult.Fail, "seek callback returned error 1")]
    [DataRow(SeekResult.CantSeek, "seek callback returned error 2")]
    public void RewindToStart_SeekDoesNotReturnOk_GivesLibcurlsTextAndStaysRead(SeekResult result, string text)
    {
        using SeekingCallbackReadStream stream = new(buffer => buffer.Length, (_, _) => result);
        _ = stream.Read(new byte[1], 0, 1);

        string? failure = stream.RewindToStart();

        Assert.AreEqual(text, failure);
        Assert.IsTrue(stream.HasBeenRead);
    }

    [TestMethod]
    public void HasBeenRead_ReadCallbackGaveOnlyTheEnd_IsFalse()
    {
        using SeekingCallbackReadStream stream = new(_ => 0, (_, _) => SeekResult.Ok);

        _ = stream.Read(new byte[1], 0, 1);

        Assert.IsFalse(stream.HasBeenRead);
    }
}
