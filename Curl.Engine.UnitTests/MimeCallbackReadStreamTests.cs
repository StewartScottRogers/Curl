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
    public void Length_SeekCallbackAndSizeZero_IsZero()
    {
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(0, _ => 0, (_, _) => SeekResult.Ok));

        Assert.AreEqual(0, stream.Length);
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
    public void Read_KnownSizeAllGiven_EndsWithoutAskingTheCallbackAgain()
    {
        int calls = 0;
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(
            2,
            buffer =>
            {
                calls++;
                buffer[0] = 7;
                return 1;
            },
            null));
        byte[] buffer = new byte[8];

        int first = stream.Read(buffer);
        int second = stream.Read(buffer);
        int end = stream.Read(buffer);

        Assert.AreEqual((1, 1, 0), (first, second, end));
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task ReadAsync_KnownSizeAllGiven_EndsWithoutAskingTheCallbackAgain()
    {
        int calls = 0;
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(
            1,
            buffer =>
            {
                calls++;
                buffer[0] = 7;
                return 1;
            },
            null));
        byte[] buffer = new byte[8];

        int first = await stream.ReadAsync(buffer.AsMemory());
        int end = await stream.ReadAsync(buffer.AsMemory());

        Assert.AreEqual((1, 0), (first, end));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void Read_UnknownSize_AsksTheCallbackUntilItReturnsZero()
    {
        int calls = 0;
        using MimeCallbackReadStream stream = new(new EasyMimeCallback(-1, _ => calls++ < 3 ? 1 : 0, null));
        byte[] buffer = new byte[8];

        int total = 0;
        while (stream.Read(buffer) is var read and > 0)
        {
            total += read;
        }

        Assert.AreEqual(3, total);
        Assert.AreEqual(4, calls);
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
