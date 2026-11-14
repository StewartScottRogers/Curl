using Curl.Core;

namespace Curl.Core.UnitTests;

[TestClass]
public sealed class ClosedStandardInputStreamBadDescriptorTests
{
    private const int BadDescriptorErrno = 9;

    [TestMethod]
    public void InPlaceOfNull_ReadsThrowIoExceptionBadDescriptor_IsClosedStandardInput()
    {
        using Stream result = ClosedStandardInputStream.InPlaceOfNull(new ThrowingStream(new IOException("Bad file descriptor") { HResult = BadDescriptorErrno }), true);

        Assert.IsInstanceOfType<ClosedStandardInputStream>(result);
        Assert.AreEqual(0, result.Read(new byte[4], 0, 4));
    }

    [TestMethod]
    public void InPlaceOfNull_ReadsThrowWrappedBadDescriptor_IsClosedStandardInput()
    {
        var wrapped = new UnauthorizedAccessException("Access to the path is denied.", new IOException("Bad file descriptor") { HResult = BadDescriptorErrno });

        using Stream result = ClosedStandardInputStream.InPlaceOfNull(new ThrowingStream(wrapped), true);

        Assert.IsInstanceOfType<ClosedStandardInputStream>(result);
    }

    [TestMethod]
    public void InPlaceOfNull_ReadsThrowOtherIoException_IsNotSwallowed()
    {
        using var stream = new ThrowingStream(new IOException("Input/output error") { HResult = 5 });

        Assert.ThrowsExactly<IOException>(() => ClosedStandardInputStream.InPlaceOfNull(stream, true));
    }

    [TestMethod]
    public void InPlaceOfNull_ReadsThrowOtherWrappedException_IsNotSwallowed()
    {
        using var stream = new ThrowingStream(new InvalidOperationException("boom", new IOException("x") { HResult = 5 }));

        Assert.ThrowsExactly<InvalidOperationException>(() => ClosedStandardInputStream.InPlaceOfNull(stream, true));
    }

    [TestMethod]
    public void InPlaceOfNull_ReadableStream_IsReturnedUnchanged()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        Assert.AreSame(stream, ClosedStandardInputStream.InPlaceOfNull(stream, true));
        Assert.AreSame(stream, ClosedStandardInputStream.InPlaceOfNull(stream));
    }

    [TestMethod]
    public void InPlaceOfNull_TerminalStandardInput_IsNotProbed()
    {
        using var stream = new ThrowingStream(new IOException("must not be read") { HResult = BadDescriptorErrno });

        Assert.AreSame(stream, ClosedStandardInputStream.InPlaceOfNull(stream, false));
    }

    [TestMethod]
    public void InPlaceOfNull_TerminalStandardInputThatIsStreamNull_IsAClosedStandardInput()
    {
        Assert.IsInstanceOfType<ClosedStandardInputStream>(ClosedStandardInputStream.InPlaceOfNull(Stream.Null, false));
    }

    private sealed class ThrowingStream(Exception toThrow) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw toThrow;
        public override int Read(Span<byte> buffer) => throw toThrow;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
