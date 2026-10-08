using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-o</c> stream: it opens its file on the first write, is write-only and
/// asynchronous-only, and settles the transfer's result the way curl 8.21.0 reports it.
/// </summary>
[TestClass]
public sealed class DeferredOutputFileStreamTests
{
    private readonly InMemoryFileSystem fileSystem = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Construct_DoesNotOpenTheFile()
    {
        Diagnostics.Arrange("file", "a.txt, truncate");

        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Act("file opened", fileSystem.Written.ContainsKey("a.txt"));

        Diagnostics.Assert("file opened", false, fileSystem.Written.ContainsKey("a.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "a.txt, truncate");

        Diagnostics.Act("capabilities", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");

        Diagnostics.Assert("capabilities", "read False, seek False, write True", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("members tried", "Length, Position get and set, Read, Seek, SetLength, Write");

        Diagnostics.Act("expected exception", nameof(NotSupportedException));

        Diagnostics.Assert("each member throws", nameof(NotSupportedException), nameof(NotSupportedException));
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
        Diagnostics.Arrange("writes", "bytes 1..2 of 01 02 03 04, then 05");

        await stream.WriteAsync([1, 2, 3, 4], 1, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 5 }.AsMemory());
        stream.Flush();
        Diagnostics.Bytes("file bytes", fileSystem.Written["a.txt"].ToArray());
        Diagnostics.Act("file length", fileSystem.Written["a.txt"].Length);

        Diagnostics.Diff("file bytes", new byte[] { 2, 3, 5 }, fileSystem.Written["a.txt"].ToArray());
        CollectionAssert.AreEqual(new byte[] { 2, 3, 5 }, fileSystem.Written["a.txt"].ToArray());
    }

    [TestMethod]
    public void Flush_BeforeAnyWrite_DoesNotOpenTheFile()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "a.txt, truncate");

        stream.Flush();
        Diagnostics.Act("file opened", fileSystem.Written.ContainsKey("a.txt"));

        Diagnostics.Assert("file opened", false, fileSystem.Written.ContainsKey("a.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task WriteAsync_UncreatableFile_ThrowsIOExceptionAndCompleteReportsTheWriteSize()
    {
        fileSystem.UnwritablePaths.Add("x");
        using DeferredOutputFileStream stream = new(fileSystem, "x", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "x, which cannot be created");
        Diagnostics.Arrange("write", "7 bytes");

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[7].AsMemory()).AsTask());
        TransferResult result = await stream.CompleteAsync(TransferResult.Failure(CurlExitCode.WriteError, "passed"));
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);

        Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("error message", "client returned ERROR on write of 7 bytes", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("client returned ERROR on write of 7 bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task WriteAsync_UncreatableFileTwice_CompleteReportsTheFirstWriteSize()
    {
        fileSystem.UnwritablePaths.Add("x");
        using DeferredOutputFileStream stream = new(fileSystem, "x", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "x, which cannot be created");
        Diagnostics.Arrange("writes", "7 bytes, then 3 bytes");

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[7].AsMemory()).AsTask());
        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[3].AsMemory()).AsTask());
        TransferResult result = await stream.CompleteAsync(TransferResult.Failure(CurlExitCode.WriteError, "passed"));
        Diagnostics.Act("error message", result.ErrorMessage);

        Diagnostics.Assert("error message", "client returned ERROR on write of 7 bytes", result.ErrorMessage);
        Assert.AreEqual("client returned ERROR on write of 7 bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenFailureWarning_BeforeAndAfterAFailedOpen_IsNullThenCurlsWarning()
    {
        InMemoryFileSystem files = new() { UnwritableStatus = FileAccessStatus.AccessDenied };
        files.UnwritablePaths.Add("C:/Windows/System32/x");
        using DeferredOutputFileStream stream = new(files, "C:/Windows/System32/x", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "C:/Windows/System32/x, access denied");

        Diagnostics.Act("warning before the write", stream.OpenFailureWarning ?? "null");
        Diagnostics.Assert("warning before the write", "null", stream.OpenFailureWarning ?? "null");
        Assert.IsNull(stream.OpenFailureWarning);
        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync(new byte[1].AsMemory()).AsTask());
        Diagnostics.Act("warning after the write", stream.OpenFailureWarning);

        Diagnostics.Assert("warning after the write", "Warning: Failed to open the file C:/Windows/System32/x: Permission denied", stream.OpenFailureWarning);
        Assert.AreEqual("Warning: Failed to open the file C:/Windows/System32/x: Permission denied", stream.OpenFailureWarning);
    }

    [TestMethod]
    public async Task OpenFailureWarning_SuccessfulOpen_IsNull()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "a.txt, writable");

        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        Diagnostics.Act("warning", stream.OpenFailureWarning ?? "null");

        Diagnostics.Assert("warning", "null", stream.OpenFailureWarning ?? "null");
        Assert.IsNull(stream.OpenFailureWarning);
    }

    [TestMethod]
    public async Task WriteAsync_CreatesTheFileWithFopensMode0666()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "a.txt, truncate");

        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        Diagnostics.Act("create modes", string.Join(", ", fileSystem.CreateModes.Select(mode => Convert.ToString((int)mode, 8))));

        Diagnostics.Assert("create mode (octal)", "666", Convert.ToString((int)fileSystem.CreateModes.Single(), 8));
        Assert.AreEqual(Convert.ToInt32("666", 8), (int)fileSystem.CreateModes.Single());
    }

    [TestMethod]
    public async Task FlushAsync_BeforeAndAfterTheFirstWrite_Succeeds()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("calls", "FlushAsync, WriteAsync 01, FlushAsync");

        await stream.FlushAsync();
        Diagnostics.Act("file opened after the first flush", fileSystem.Written.ContainsKey("a.txt"));
        Diagnostics.Assert("file opened after the first flush", false, fileSystem.Written.ContainsKey("a.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        await stream.FlushAsync();
        Diagnostics.Bytes("file bytes", fileSystem.Written["a.txt"].ToArray());

        Diagnostics.Diff("file bytes", new byte[] { 1 }, fileSystem.Written["a.txt"].ToArray());
        CollectionAssert.AreEqual(new byte[] { 1 }, fileSystem.Written["a.txt"].ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_OpenFile_ClosesIt()
    {
        DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        Diagnostics.Arrange("file", "a.txt, opened by a write");

        await stream.DisposeAsync();
        Diagnostics.Act("file can write", fileSystem.Written["a.txt"].CanWrite);

        Diagnostics.Assert("file can write", false, fileSystem.Written["a.txt"].CanWrite);
        Assert.IsFalse(fileSystem.Written["a.txt"].CanWrite);
    }

    [TestMethod]
    public async Task DisposeAsync_NoFile_OpensNothing()
    {
        DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        Diagnostics.Arrange("file", "a.txt, never written");

        await stream.DisposeAsync();
        Diagnostics.Act("file opened", fileSystem.Written.ContainsKey("a.txt"));

        Diagnostics.Assert("file opened", false, fileSystem.Written.ContainsKey("a.txt"));
        Assert.IsFalse(fileSystem.Written.ContainsKey("a.txt"));
    }

    [TestMethod]
    public async Task CompleteAsync_AfterWrites_ReturnsTheHandlersResult()
    {
        using DeferredOutputFileStream stream = new(fileSystem, "a.txt", FileWriteMode.Truncate);
        await stream.WriteAsync(new byte[] { 1 }.AsMemory());
        TransferResult handlerResult = TransferResult.Success(1);
        Diagnostics.Arrange("handler result", handlerResult.ExitCode);

        TransferResult result = await stream.CompleteAsync(handlerResult);
        Diagnostics.Act("result is the handler's", ReferenceEquals(handlerResult, result));

        Diagnostics.Assert("result is the handler's", true, ReferenceEquals(handlerResult, result));
        Assert.AreSame(handlerResult, result);
    }
}
