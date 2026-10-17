using Curl.Api.Callbacks;
using Curl.Api.Options.Http;

namespace Curl.Engine;

[TestClass]
public sealed class MimeCallbackReadStreamTests
{
    [TestMethod]
    public void CanSeek_NoSeekCallback_IsFalseAndLengthPositionAndSeekThrow()
    {
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(3, _ => 0, null));

        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
    }

    [TestMethod]
    public void Length_SeekCallbackAndKnownSize_IsTheSize()
    {
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(3, _ => 0, (_, _) => SeekResult.Ok));

        Assert.IsTrue(stream.CanSeek);
        Assert.AreEqual(3, stream.Length);
    }

    [TestMethod]
    public void Length_SeekCallbackAndUnknownSize_Throws()
    {
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(-1, _ => 0, (_, _) => SeekResult.Ok));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
    }

    [TestMethod]
    public void Position_SetToZeroAfterAReadAndToWhereItIs_CallsTheSeekCallbackOnlyWhenItMustMove()
    {
        List<(long Offset, SeekOrigin Origin)> seeks = [];
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(
            2,
            buffer =>
            {
                buffer[0] = 7;
                return 1;
            },
            (offset, origin) =>
            {
                seeks.Add((offset, origin));
                return SeekResult.Ok;
            }));

        stream.Position = 0;
        Assert.IsEmpty(seeks);
        Assert.AreEqual(7, stream.ReadByte());
        Assert.AreEqual(1, stream.Position);
        stream.Position = 1;
        Assert.IsEmpty(seeks);
        stream.Position = 0;

        Assert.AreEqual((0L, SeekOrigin.Begin), seeks.Single());
        Assert.AreEqual(0, stream.Position);
    }

    [TestMethod]
    [DataRow(SeekResult.Fail, "seek callback returned error 1")]
    [DataRow(SeekResult.CantSeek, "seek callback returned error 2")]
    public void Position_SeekCallbackFails_ThrowsIOExceptionWithLibcurlsText(SeekResult answer, string expected)
    {
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(2, buffer => 1, (_, _) => answer));
        _ = stream.ReadByte();

        IOException failure = Assert.ThrowsExactly<IOException>(() => stream.Position = 0);

        Assert.AreEqual(expected, failure.Message);
        Assert.AreEqual(1, stream.Position);
    }
}
