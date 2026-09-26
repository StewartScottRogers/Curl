using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// Pins how standard output is opened: a console through the console stream, a closed
/// handle as a stream whose writes fail, and anything else as an unbuffered stream over the
/// handle that reports a reader that has gone - none of it using this process's own
/// standard handles.
/// </summary>
[TestClass]
public sealed class StandardOutputOpenerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Open_Console_OpensTheConsoleStream()
    {
        using MemoryStream console = new();

        Stream opened = StandardOutputOpener.Open(isOutputRedirected: false, () => console, standardOutputHandleValue: 0);

        Assert.AreSame(console, opened);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Open_RedirectedToAClosedHandle_OpensAStreamWhoseWritesFail(int handleValue)
    {
        using Stream opened = StandardOutputOpener.Open(isOutputRedirected: true, () => new MemoryStream(), handleValue);

        Assert.IsInstanceOfType<ClosedStandardOutputStream>(opened);
    }

    [TestMethod]
    public void OpenHandle_File_WritesEachChunkBeforeTheNextIsIssued()
    {
        string path = Path.Combine(TestContext.TestRunResultsDirectory ?? Path.GetTempPath(), $"{Guid.NewGuid():N}.out");

        try
        {
            using SafeFileHandle file = File.OpenHandle(path, FileMode.CreateNew, FileAccess.Write);
            using Stream opened = StandardOutputOpener.OpenHandle(file.DangerousGetHandle());

            opened.Write([1, 2, 3], 0, 3);
            long lengthAfterFirstWrite = RandomAccess.GetLength(file);
            opened.Write([4], 0, 1);

            Assert.AreEqual(3, lengthAfterFirstWrite);
            Assert.AreEqual(4, RandomAccess.GetLength(file));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void OpenHandle_DoesNotOwnTheHandle()
    {
        using AnonymousPipeServerStream reader = new(PipeDirection.In);
        using AnonymousPipeClientStream writer = new(PipeDirection.Out, reader.ClientSafePipeHandle);

        StandardOutputOpener.OpenHandle(writer.SafePipeHandle.DangerousGetHandle()).Dispose();
        writer.Write([9], 0, 1);

        Assert.AreEqual(9, reader.ReadByte());
    }

    [TestMethod]
    public async Task OpenHandle_PipeWhoseReaderHasGone_WriteThrowsIOException()
    {
        using AnonymousPipeServerStream writer = new(PipeDirection.Out);
        writer.DisposeLocalCopyOfClientHandle();
        using Stream opened = StandardOutputOpener.OpenHandle(writer.SafePipeHandle.DangerousGetHandle());

        await Assert.ThrowsAsync<IOException>(async () => await opened.WriteAsync(new byte[16384], TestContext.CancellationToken));
    }

    [TestMethod]
    public void OpenHandle_OpensTheStreamOverTheHandleWithoutOwningIt()
    {
        using MemoryStream handleStream = new();
        SafeFileHandle? given = null;

        Stream opened = StandardOutputOpener.OpenHandle(
            4321,
            handle =>
            {
                given = handle;

                return handleStream;
            });

        Assert.AreSame(handleStream, opened);
        Assert.AreEqual((nint)4321, given!.DangerousGetHandle());
        Assert.IsFalse(given.IsInvalid);
        given.Dispose();
    }

    [TestMethod]
    public void OpenHandle_HandleThatCannotBeOpened_OpensAStreamWhoseWritesFail()
    {
        using Stream opened = StandardOutputOpener.OpenHandle(4321, _ => throw new IOException("The handle is invalid."));

        Assert.IsInstanceOfType<ClosedStandardOutputStream>(opened);
    }

    [TestMethod]
    public void ReadStandardOutputHandleValue_Windows_AsksWindowsForTheHandle()
    {
        nint handleValue = StandardOutputOpener.ReadStandardOutputHandleValue(isWindows: true, () => 1234);

        Assert.AreEqual((nint)1234, handleValue);
    }

    [TestMethod]
    public void ReadStandardOutputHandleValue_Elsewhere_IsFileDescriptorOne()
    {
        nint handleValue = StandardOutputOpener.ReadStandardOutputHandleValue(
            isWindows: false,
            () => throw new AssertFailedException("Windows was asked for a handle."));

        Assert.AreEqual((nint)StandardOutputOpener.UnixStandardOutputFileDescriptor, handleValue);
    }

    [TestMethod]
    public void Open_ThisProcess_OpensAWritableStream()
    {
        using Stream opened = StandardOutputOpener.Open();

        Assert.IsTrue(opened.CanWrite);
    }
}
