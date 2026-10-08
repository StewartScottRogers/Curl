using System.IO.Pipes;
using Curl.Testing;
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

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Open_Console_OpensTheConsoleStream()
    {
        using MemoryStream console = new();
        Diagnostics.Arrange("output redirected / handle", "False / 0");

        Stream opened = StandardOutputOpener.Open(isOutputRedirected: false, () => console, standardOutputHandleValue: 0);
        Diagnostics.Act("opened the console stream", ReferenceEquals(console, opened));

        Diagnostics.Assert("opened the console stream", true, ReferenceEquals(console, opened));
        Assert.AreSame(console, opened);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Open_RedirectedToAClosedHandle_OpensAStreamWhoseWritesFail(int handleValue)
    {
        Diagnostics.Arrange("output redirected / handle", $"True / {handleValue}");
        using Stream opened = StandardOutputOpener.Open(isOutputRedirected: true, () => new MemoryStream(), handleValue);
        Diagnostics.Act("opened", opened.GetType().Name);

        Diagnostics.Assert("opened", nameof(ClosedStandardOutputStream), opened.GetType().Name);
        Assert.IsInstanceOfType<ClosedStandardOutputStream>(opened);
    }

    [TestMethod]
    public void OpenHandle_File_WritesEachChunkBeforeTheNextIsIssued()
    {
        string path = Path.Combine(TestContext.TestRunResultsDirectory ?? Path.GetTempPath(), $"{Guid.NewGuid():N}.out");
        Diagnostics.Arrange("handle", "a new file in the test results directory");
        Diagnostics.Arrange("writes", "[1,2,3], then [4]");

        try
        {
            using SafeFileHandle file = File.OpenHandle(path, FileMode.CreateNew, FileAccess.Write);
            using Stream opened = StandardOutputOpener.OpenHandle(file.DangerousGetHandle());

            opened.Write([1, 2, 3], 0, 3);
            long lengthAfterFirstWrite = RandomAccess.GetLength(file);
            opened.Write([4], 0, 1);
            long lengthAfterSecondWrite = RandomAccess.GetLength(file);
            Diagnostics.Act("length after first / second write", $"{lengthAfterFirstWrite} / {lengthAfterSecondWrite}");

            Diagnostics.Assert("length after first / second write", "3 / 4", $"{lengthAfterFirstWrite} / {lengthAfterSecondWrite}");
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
        Diagnostics.Arrange("handle", "the writing end of an anonymous pipe");

        StandardOutputOpener.OpenHandle(writer.SafePipeHandle.DangerousGetHandle()).Dispose();
        writer.Write([9], 0, 1);
        int read = reader.ReadByte();
        Diagnostics.Act("byte read through the pipe after disposing", read);

        Diagnostics.Assert("byte read through the pipe after disposing", 9, read);
        Assert.AreEqual(9, read);
    }

    [TestMethod]
    public async Task OpenHandle_PipeWhoseReaderHasGone_WriteThrowsIOException()
    {
        using AnonymousPipeServerStream writer = new(PipeDirection.Out);
        writer.DisposeLocalCopyOfClientHandle();
        using Stream opened = StandardOutputOpener.OpenHandle(writer.SafePipeHandle.DangerousGetHandle());
        Diagnostics.Arrange("handle / write size", "a pipe whose reader has gone / 16384");

        IOException exception = await Assert.ThrowsAsync<IOException>(async () => await opened.WriteAsync(new byte[16384], TestContext.CancellationToken));
        Diagnostics.Act("exception is an IOException", exception is not null);

        Diagnostics.Assert("exception is an IOException", true, exception is not null);
    }

    [TestMethod]
    public void OpenHandle_OpensTheStreamOverTheHandleWithoutOwningIt()
    {
        using MemoryStream handleStream = new();
        SafeFileHandle? given = null;
        Diagnostics.Arrange("handle", 4321);

        Stream opened = StandardOutputOpener.OpenHandle(
            4321,
            handle =>
            {
                given = handle;

                return handleStream;
            });
        Diagnostics.Act("opened the handle stream", ReferenceEquals(handleStream, opened));
        Diagnostics.Act("handle given / invalid", $"{given!.DangerousGetHandle()} / {given.IsInvalid}");

        Diagnostics.Assert("handle given / invalid", "4321 / False", $"{given.DangerousGetHandle()} / {given.IsInvalid}");
        Assert.AreSame(handleStream, opened);
        Assert.AreEqual((nint)4321, given!.DangerousGetHandle());
        Assert.IsFalse(given.IsInvalid);
        given.Dispose();
    }

    [TestMethod]
    public void OpenHandle_HandleThatCannotBeOpened_OpensAStreamWhoseWritesFail()
    {
        Diagnostics.Arrange("handle / opening it", "4321 / throws IOException");
        using Stream opened = StandardOutputOpener.OpenHandle(4321, _ => throw new IOException("The handle is invalid."));
        Diagnostics.Act("opened", opened.GetType().Name);

        Diagnostics.Assert("opened", nameof(ClosedStandardOutputStream), opened.GetType().Name);
        Assert.IsInstanceOfType<ClosedStandardOutputStream>(opened);
    }

    [TestMethod]
    public void ReadStandardOutputHandleValue_Windows_AsksWindowsForTheHandle()
    {
        Diagnostics.Arrange("is Windows / Windows answers", "True / 1234");
        nint handleValue = StandardOutputOpener.ReadStandardOutputHandleValue(isWindows: true, () => 1234);
        Diagnostics.Act("handle value", handleValue);

        Diagnostics.Assert("handle value", (nint)1234, handleValue);
        Assert.AreEqual((nint)1234, handleValue);
    }

    [TestMethod]
    public void ReadStandardOutputHandleValue_Elsewhere_IsFileDescriptorOne()
    {
        Diagnostics.Arrange("is Windows", false);
        nint handleValue = StandardOutputOpener.ReadStandardOutputHandleValue(
            isWindows: false,
            () => throw new AssertFailedException("Windows was asked for a handle."));
        Diagnostics.Act("handle value", handleValue);

        Diagnostics.Assert("handle value", (nint)StandardOutputOpener.UnixStandardOutputFileDescriptor, handleValue);
        Assert.AreEqual((nint)StandardOutputOpener.UnixStandardOutputFileDescriptor, handleValue);
    }

    [TestMethod]
    public void Open_ThisProcess_OpensAWritableStream()
    {
        Diagnostics.Arrange("standard output", "this test host's");
        using Stream opened = StandardOutputOpener.Open();
        Diagnostics.Act("can write", opened.CanWrite);

        Diagnostics.Assert("can write", true, opened.CanWrite);
        Assert.IsTrue(opened.CanWrite);
    }
}
