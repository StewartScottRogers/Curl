using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", name);
        diagnostics.Arrange("source read size", sourceReadSize);
        MultipartPartEncoder encoder = MultipartPartEncoder.Find(name)!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new SplitReadStream(Data, sourceReadSize), Data.Length);

        using MemoryStream copy = new();
        await stream.CopyToAsync(copy, TestContext.CancellationToken);

        diagnostics.Act("encoded bytes read", copy.Length);
        diagnostics.Diff("encoded body", encoder.Encode(Data)!, copy.ToArray());
        CollectionAssert.AreEqual(encoder.Encode(Data), copy.ToArray());
    }

    [TestMethod]
    public void ReadsSynchronouslyAsItReadsAsynchronously()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "quoted-printable");
        diagnostics.Arrange("source read size", 13);
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("quoted-printable")!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new SplitReadStream(Data, 13), Data.Length);

        using MemoryStream copy = new();
        byte[] buffer = new byte[10];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            copy.Write(buffer, 0, read);
        }

        diagnostics.Act("encoded bytes read", copy.Length);
        diagnostics.Diff("encoded body", encoder.Encode(Data)!, copy.ToArray());
        CollectionAssert.AreEqual(encoder.Encode(Data), copy.ToArray());
        int afterEnd = stream.Read(buffer, 0, buffer.Length);
        diagnostics.Act("read after the end", afterEnd);
        diagnostics.Assert("read after the end", 0, afterEnd);
        Assert.AreEqual(0, afterEnd, "The end stays the end.");
    }

    [TestMethod]
    public async Task ReadsIntoAnArrayAsynchronously()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "base64");
        diagnostics.Arrange("source", "[1,2,3]");
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new MemoryStream([1, 2, 3]), 3);
        byte[] buffer = new byte[8];

        int read = await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.CancellationToken);

        string text = Encoding.ASCII.GetString(buffer, 0, read);
        diagnostics.Act("bytes read", read);
        diagnostics.Bytes("encoded", buffer.AsSpan(0, read));
        diagnostics.Diff("encoded text", "AQID", text);
        Assert.AreEqual("AQID", text);
    }

    [TestMethod]
    public void ARefusedByteFailsTheReadThatReachesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "7bit");
        diagnostics.Arrange("source", "[0x41, 0xE9]");
        using EncodedReadStream stream = MultipartPartEncoder.Find("7bit")!.EncodeWhileReading(new MemoryStream([0x41, 0xE9]), 2);

        RequestBodyReadFailedException refused = Assert.ThrowsExactly<RequestBodyReadFailedException>(() => stream.ReadByte());

        diagnostics.Act("exception", $"{refused.GetType().Name}: {refused.Message}");
        diagnostics.Assert("message", MultipartFormBodyBuilder.ReadFailedMessage, refused.Message);
        Assert.IsInstanceOfType<IOException>(refused);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, refused.Message);
    }

    [TestMethod]
    public void SeeksByEncodingAgainFromWhereTheSourceStood()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "base64");
        diagnostics.Arrange("source start position", 2);
        MemoryStream source = new([9, 9, .. Data]) { Position = 2 };
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("base64")!;
        byte[] expected = encoder.Encode(Data)!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(source, Data.Length);

        diagnostics.Act("CanRead", stream.CanRead);
        diagnostics.Act("CanSeek", stream.CanSeek);
        diagnostics.Act("CanWrite", stream.CanWrite);
        diagnostics.Assert("CanRead", true, stream.CanRead);
        Assert.IsTrue(stream.CanRead);
        diagnostics.Assert("CanSeek", true, stream.CanSeek);
        Assert.IsTrue(stream.CanSeek);
        diagnostics.Assert("CanWrite", false, stream.CanWrite);
        Assert.IsFalse(stream.CanWrite);
        diagnostics.Assert("Length", expected.Length, stream.Length);
        Assert.AreEqual(expected.Length, stream.Length);
        _ = stream.Read(new byte[100], 0, 100);
        diagnostics.Assert("Position after 100 bytes", 100, stream.Position);
        Assert.AreEqual(100, stream.Position);

        long relative = stream.Seek(-10, SeekOrigin.Current);
        diagnostics.Act("seek -10 from current", relative);
        diagnostics.Assert("seek -10 from current", 90, relative);
        Assert.AreEqual(90, relative);
        Assert.AreEqual(expected[90], stream.ReadByte());
        long fromBegin = stream.Seek(5, SeekOrigin.Begin);
        diagnostics.Act("seek 5 from begin", fromBegin);
        diagnostics.Assert("seek 5 from begin", 5, fromBegin);
        Assert.AreEqual(5, fromBegin);
        Assert.AreEqual(expected[5], stream.ReadByte());
        long fromEnd = stream.Seek(-1, SeekOrigin.End);
        diagnostics.Act("seek -1 from end", fromEnd);
        diagnostics.Assert("seek -1 from end", expected.Length - 1, fromEnd);
        Assert.AreEqual(expected.Length - 1, fromEnd);
        Assert.AreEqual(expected[^1], stream.ReadByte());
        int atEnd = stream.ReadByte();
        diagnostics.Assert("byte at end", -1, atEnd);
        Assert.AreEqual(-1, atEnd);

        stream.Position = 0;
        Assert.AreEqual(expected[0], stream.ReadByte());
        stream.Position = expected.Length + 10;
        diagnostics.Act("Position past the end", stream.Position);
        diagnostics.Assert("Position past the end", expected.Length, stream.Position);
        Assert.AreEqual(expected.Length, stream.Position, "A position past the end stops at the end.");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Position = -1);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)3));
    }

    [TestMethod]
    public void AnUnknownLengthIsMeasuredByEncodingOnceAndThePositionKept()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "quoted-printable");
        diagnostics.Arrange("bytes read before measuring", 30);
        MultipartPartEncoder encoder = MultipartPartEncoder.Find("quoted-printable")!;
        using EncodedReadStream stream = encoder.EncodeWhileReading(new MemoryStream(Data), Data.Length);
        _ = stream.Read(new byte[30], 0, 30);

        diagnostics.Act("Length", stream.Length);
        diagnostics.Assert("Length", encoder.Encode(Data)!.Length, stream.Length);
        Assert.AreEqual(encoder.Encode(Data)!.Length, stream.Length);
        diagnostics.Assert("Position", 30, stream.Position);
        Assert.AreEqual(30, stream.Position);
        Assert.AreEqual(encoder.Encode(Data)![30], stream.ReadByte());
    }

    [TestMethod]
    public void ASourceThatCannotSeekCannotBeSeekedOrMeasured()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "base64");
        diagnostics.Arrange("source", "cannot seek");
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new SplitReadStream(Data, 10, seeks: false), null);

        diagnostics.Act("CanSeek", stream.CanSeek);
        diagnostics.Assert("CanSeek", false, stream.CanSeek);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
    }

    [TestMethod]
    public void WritesAreRefusedAndFlushDoesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "base64");
        diagnostics.Arrange("source", "empty");
        using EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(new MemoryStream(), 0);

        stream.Flush();
        NotSupportedException setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(1));
        diagnostics.Act("SetLength exception", setLength.GetType().Name);
        diagnostics.Assert("SetLength exception", nameof(NotSupportedException), setLength.GetType().Name);
        NotSupportedException write = Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        diagnostics.Act("Write exception", write.GetType().Name);
        diagnostics.Assert("Write exception", nameof(NotSupportedException), write.GetType().Name);
        diagnostics.Assert("Length", 0, stream.Length);
        Assert.AreEqual(0, stream.Length);
        int read = stream.ReadByte();
        diagnostics.Act("ReadByte", read);
        diagnostics.Assert("ReadByte", -1, read);
        Assert.AreEqual(-1, read);
    }

    [TestMethod]
    public void DisposingTheStreamDisposesItsSource()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "base64");
        SplitReadStream source = new(Data, 10);
        EncodedReadStream stream = MultipartPartEncoder.Find("base64")!.EncodeWhileReading(source, Data.Length);

        stream.Dispose();

        ObjectDisposedException thrown = Assert.ThrowsExactly<ObjectDisposedException>(() => source.ReadByte());
        diagnostics.Act("source read exception", thrown.GetType().Name);
        diagnostics.Assert("source read exception", nameof(ObjectDisposedException), thrown.GetType().Name);
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
