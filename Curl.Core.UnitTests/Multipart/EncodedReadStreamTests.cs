using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins <see cref="EncodedReadStream" />: the bytes it reads are the bytes
/// <see cref="MultipartPartEncoder.Encode" /> gives for the whole data, however the source
/// splits its reads, and it seeks by encoding again from the source's start.
/// </summary>
[TestClass]
public sealed class EncodedReadStreamTests
{
    private static readonly byte[] Data = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("The quick brown fox = jumps\tover \r\nthe lazy dög \n", 40)) + "end ");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("base64", 1)]
    [DataRow("base64", 3)]
    [DataRow("base64", 100_000)]
    [DataRow("quoted-printable", 1)]
    [DataRow("quoted-printable", 5)]
    [DataRow("quoted-printable", 100_000)]
    [DataRow("8bit", 7)]
    public async Task ReadsTheSameBytesAsEncodingTheDataWholeHoweverTheSourceSplitsIt(string name, int sourceReadSize)
    {
        MultipartPartEncoder encoder = MultipartPartEncoder.Find(name)!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new SplitReadStream(Data, sourceReadSize), Data.Length);

        using MemoryStream copy = new();
        await stream.CopyToAsync(copy, TestContext.CancellationToken);

        CollectionAssert.AreEqual(encoder.Encode(Data), copy.ToArray());
    }

    [TestMethod]
    public void ReadsSynchronouslyAsItReadsAsynchronously()
    {
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("quoted-printable")!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new SplitReadStream(Data, 13), Data.Length);

        using MemoryStream copy = new();
        byte[] buffer = new byte[10];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            copy.Write(buffer, 0, read);
        }

        CollectionAssert.AreEqual(encoder.Encode(Data), copy.ToArray());
        Assert.AreEqual(0, stream.Read(buffer, 0, buffer.Length), "The end stays the end.");
    }

    [TestMethod]
    public async Task ReadsIntoAnArrayAsynchronously()
    {
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new MemoryStream([1, 2, 3]), 3);
        byte[] buffer = new byte[8];

        int read = await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.CancellationToken);

        Assert.AreEqual("AQID", Encoding.ASCII.GetString(buffer, 0, read));
    }

    [TestMethod]
    public void ARefusedByteFailsTheReadThatReachesIt()
    {
        using EncodedReadStream stream = MultipartPartEncoder.Find("7bit")!.EncodeWhileReading(new MemoryStream([0x41, 0xE9]), 2);

        MultipartDataRefusedException refused = Assert.ThrowsExactly<MultipartDataRefusedException>(() => stream.ReadByte());

        Assert.IsInstanceOfType<IOException>(refused);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, refused.Message);
    }

    [TestMethod]
    public void SeeksByEncodingAgainFromWhereTheSourceStood()
    {
        MemoryStream source = new([9, 9, .. Data]) { Position = 2 };
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("base64")!;
        byte[] expected = encoder.Encode(Data)!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(source, Data.Length);

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.AreEqual(expected.Length, stream.Length);
        _ = stream.Read(new byte[100], 0, 100);
        Assert.AreEqual(100, stream.Position);

        Assert.AreEqual(90, stream.Seek(-10, SeekOrigin.Current));
        Assert.AreEqual(expected[90], stream.ReadByte());
        Assert.AreEqual(5, stream.Seek(5, SeekOrigin.Begin));
        Assert.AreEqual(expected[5], stream.ReadByte());
        Assert.AreEqual(expected.Length - 1, stream.Seek(-1, SeekOrigin.End));
        Assert.AreEqual(expected[^1], stream.ReadByte());
        Assert.AreEqual(-1, stream.ReadByte());

        stream.Position = 0;
        Assert.AreEqual(expected[0], stream.ReadByte());
        stream.Position = expected.Length + 10;
        Assert.AreEqual(expected.Length, stream.Position, "A position past the end stops at the end.");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Position = -1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)3));
    }

    [TestMethod]
    public void AnUnknownLengthIsMeasuredByEncodingOnceAndThePositionKept()
    {
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("quoted-printable")!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new MemoryStream(Data), Data.Length);
        _ = stream.Read(new byte[30], 0, 30);

        Assert.AreEqual(encoder.Encode(Data)!.Length, stream.Length);
        Assert.AreEqual(30, stream.Position);
        Assert.AreEqual(encoder.Encode(Data)![30], stream.ReadByte());
    }

    [TestMethod]
    public void ASourceThatCannotSeekCannotBeSeekedOrMeasured()
    {
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new SplitReadStream(Data, 10, seeks: false), null);

        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
    }

    [TestMethod]
    public void WritesAreRefusedAndFlushDoesNothing()
    {
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new MemoryStream(), 0);

        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.AreEqual(0, stream.Length);
        Assert.AreEqual(-1, stream.ReadByte());
    }

    [TestMethod]
    public void DisposingTheStreamDisposesItsSource()
    {
        SplitReadStream source = new(Data, 10);
        EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(source, Data.Length);

        stream.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => source.ReadByte());
    }

    /// <summary>A memory stream that returns at most a chosen number of bytes from each read, and may refuse to seek.</summary>
    private sealed class SplitReadStream(byte[] content, int readSize, bool seeks = true) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => seeks && base.CanSeek;

        public override long Position
        {
            get => base.Position;
            set => base.Position = seeks ? value : throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, readSize)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, readSize)], cancellationToken);
    }
}
