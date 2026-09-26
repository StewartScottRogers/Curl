using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-o</c> stream: it opens its file on the first write, is write-only and
/// asynchronous-only, and settles the transfer's result the way curl 8.21.0 reports it.
/// </summary>
[TestClass]
public sealed class DeferredOutputFileStreamTests
{
    private readonly InMemoryFileSystem fileSystem = new();

    [TestMethod]
    public void Construct_DoesNotOpenTheFile()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_WritesTheSliceToTheFile()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        await stream.WriteAsync([1, 2, 3, 4], 1, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 5 }.AsMemory());
        stream.Flush();

        CollectionAssert.AreEqual(new byte[] { 2, 3, 5 }, fileSystem.Written["a.txt"].ToArray());
    }

    [TestMethod]
    public void Flush_BeforeAnyWrite_DoesNotOpenTheFile()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        stream.Flush();

        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task WriteAsync_UncreatableFile_ThrowsIOExceptionAndCompleteReportsTheWriteSize()
    {
        fileSystem.UnwritablePaths.Add("x");
        using DeferredOutputFileStream stream = new(fileSystem, "x", FileWriteMode.Truncate);

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[7].AsMemory()).AsTask());
        TransferResult result = await stream.CompleteAsync(TransferResult.Failure(CurlExitCode.WriteError, "passed"));

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("client returned ERROR on write of 7 bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task WriteAsync_UncreatableFileTwice_CompleteReportsTheFirstWriteSize()
    {
        fileSystem.UnwritablePaths.Add("x");
        using DeferredOutputFileStream stream = new(fileSystem, "x", FileWriteMode.Truncate);

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[7].AsMemory()).AsTask());
        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[3].AsMemory()).AsTask());
        TransferResult result = await stream.CompleteAsync(TransferResult.Failure(CurlExitCode.WriteError, "passed"));

        Assert.AreEqual("client returned ERROR on write of 7 bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenFailureWarning_BeforeAndAfterAFailedOpen_IsNullThenCurlsWarning()
    {
        InMemoryFileSystem files = new() { UnwritableStatus = FileAccessStatus.AccessDenied };
        files.UnwritablePaths.Add("C:/Windows/System32/x");
        using DeferredOutputFileStream stream = new(files, "C:/Windows/System32/x", FileWriteMode.Truncate);

        Assert.IsNull(stream.OpenFailureWarning);
        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[1].AsMemory()).AsTask());

        Assert.AreEqual("Warning: Failed to open the file C:/Windows/System32/x: Permission denied", stream.OpenFailureWarning);
    }

    [TestMethod]
    public async Task OpenFailureWarning_SuccessfulOpen_IsNull()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        await stream.WriteAsync(new byte[] { 1 }.AsMemory());

        Assert.IsNull(stream.OpenFailureWarning);
    }

    [TestMethod]
    public async Task WriteAsync_CreatesTheFileWithFopensMode0666()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        await stream.WriteAsync(new byte[] { 1 }.AsMemory());

        Assert.AreEqual(Convert.ToInt32("666", 8), (int)fileSystem.CreateModes.Single());
    }

    [TestMethod]
    public async Task FlushAsync_BeforeAndAfterTheFirstWrite_Succeeds()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        await stream.FlushAsync();
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        await stream.FlushAsync();

        CollectionAssert.AreEqual(new byte[] { 1 }, fileSystem.Written["a.txt"].ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_OpenFile_ClosesIt()
    {
        DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());

        await stream.DisposeAsync();

        Assert.IsFalse(fileSystem.Written["a.txt"].CanWrite);
    }

    [TestMethod]
    public async Task DisposeAsync_NoFile_OpensNothing()
    {
        DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);

        await stream.DisposeAsync();

        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task CompleteAsync_AfterWrites_ReturnsTheHandlersResult()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        TransferResult handlerResult = TransferResult.Success(1);

        TransferResult result = await stream.CompleteAsync(handlerResult);

        Assert.AreSame(handlerResult, result);
    }
}
