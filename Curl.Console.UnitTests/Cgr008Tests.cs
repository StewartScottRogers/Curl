using System.Text;

namespace Curl.Console;

/// <summary>
/// CGR-008 (BL-3537): standard error on a full device (<c>2&gt;/dev/full</c>) makes every write
/// fail with ENOSPC; curl ignores that and exits with the transfer's own code, where Curl
/// crashed. <see cref="WriteFailureIgnoringStream" /> is what <c>Program.Main</c> wraps
/// standard error in.
/// </summary>
[TestClass]
public sealed class Cgr008Tests
{
    [TestMethod]
    public async Task RunAsync_WhenStandardErrorIsFull_ReturnsTheTransfersExitCode()
    {
        using var standardOutput = new MemoryStream();
        using var standardError = new WriteFailureIgnoringStream(new FullDeviceStream());

        var exitCode = await UnhandledExceptionGuard.RunAsync(
            async () =>
            {
                await standardError.WriteAsync(Encoding.UTF8.GetBytes("curl: (7) Failed to connect\n"));
                await standardError.FlushAsync();
                return 7;
            },
            standardOutput,
            standardError);

        Assert.AreEqual(7, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_WhenTheRunThrowsAndStandardErrorIsFull_ReturnsExit2()
    {
        using var standardOutput = new MemoryStream();
        using var standardError = new WriteFailureIgnoringStream(new FullDeviceStream());

        var exitCode = await UnhandledExceptionGuard.RunAsync(
            () => throw new InvalidOperationException("handler broke"), standardOutput, standardError);

        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public void SynchronousWritesAndFlush_WhenStandardErrorIsFull_DoNotThrow()
    {
        using var standardError = new WriteFailureIgnoringStream(new FullDeviceStream());

        standardError.Write([1, 2, 3], 0, 3);
        standardError.Write([4]);
        standardError.Flush();
    }

    [TestMethod]
    public async Task ArrayWriteAsync_WhenStandardErrorIsFull_DoesNotThrow()
    {
        using var standardError = new WriteFailureIgnoringStream(new FullDeviceStream());

        await standardError.WriteAsync([1, 2, 3], 0, 3, CancellationToken.None);
    }

    [TestMethod]
    public async Task EveryWrite_WhenStandardErrorWorks_PassesTheBytesThrough()
    {
        using var inner = new MemoryStream();
        using var standardError = new WriteFailureIgnoringStream(inner);

        standardError.Write([1, 2, 3], 1, 2);
        standardError.Write([4]);
        standardError.Flush();
        await standardError.WriteAsync([5, 6], 0, 2, CancellationToken.None);
        await standardError.WriteAsync(new byte[] { 7 }.AsMemory());
        await standardError.FlushAsync();

        CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 5, 6, 7 }, inner.ToArray());
    }

    [TestMethod]
    public void Dispose_DisposesTheInnerStream()
    {
        var inner = new MemoryStream();

        new WriteFailureIgnoringStream(inner).Dispose();

        Assert.IsFalse(inner.CanWrite);
    }

    [TestMethod]
    public void TheStream_IsWriteOnlyAndUnseekable()
    {
        using var standardError = new WriteFailureIgnoringStream(new MemoryStream());

        Assert.IsFalse(standardError.CanRead);
        Assert.IsFalse(standardError.CanSeek);
        Assert.IsTrue(standardError.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => standardError.SetLength(0));
    }

    // A device with no space left: every write and flush fails as ENOSPC does.
    private sealed class FullDeviceStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new IOException("No space left on device");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("No space left on device");
    }
}
