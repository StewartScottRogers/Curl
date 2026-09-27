using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="RemoteHeaderNameStream" /> reads header lines for the <c>-J</c> name and
/// passes them on; the runner tests pin it end to end against measured curl 8.21.0.
/// </summary>
[TestClass]
public sealed class RemoteHeaderNameStreamTests
{
    private const string Disposition = "Content-Disposition: attachment; filename=x.txt\r\n";

    private readonly InMemoryFileSystem files = new();
    private readonly MemoryStream passedOn = new();

    [TestMethod]
    public async Task WriteAsync_NameInOkResponse_OpensTheNamedFileAndPassesTheLinesOn()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => "od/" + name);
        string head = "HTTP/1.1 200 OK\r\n" + Disposition + "\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));

        Assert.AreEqual("od/x.txt", output.Path);
        Assert.IsTrue(output.IsOpen);
        Assert.AreEqual(head, Encoding.ASCII.GetString(passedOn.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_SecondName_IsIgnored()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("Content-Disposition: attachment; filename=y.txt\r\n"));

        Assert.AreEqual("x.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_StatusLineWithoutCode_IgnoresTheName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 \r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 xyz\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 "));
        await stream.WriteAsync(Encoding.ASCII.GetBytes(Disposition));

        Assert.AreEqual("u.txt", output.Path);
        Assert.IsFalse(output.IsOpen);
    }

    [TestMethod]
    public async Task WriteAsync_NoStatusLine_IgnoresTheName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(Disposition));

        Assert.AreEqual("u.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_NameTaken_ThrowsIOException()
    {
        files.ExistingPaths.Add("x.txt");
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => name);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition)));

        Assert.AreEqual("Warning: Failed to open the file x.txt: File exists", output.OpenFailureWarning);
        Assert.AreEqual(0, passedOn.Length);
    }

    [TestMethod]
    public async Task WriteAsync_FileCreatedAfterTheNameIsChosen_IsNotOverwritten()
    {
        string? chosen = null;
        files.BeforeCreateNew = path => files.ExistingPaths.Add(path);
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => chosen = name);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition)));

        Assert.AreEqual("x.txt", chosen);
        CollectionAssert.AreEqual(new[] { FileWriteMode.CreateNew }, files.WriteModes);
        Assert.AreEqual(0, files.Written.Count);
        Assert.AreEqual("Warning: Failed to open the file x.txt: File exists", output.OpenFailureWarning);
        TransferResult result = await output.CompleteAsync(TransferResult.Success(0));
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("client returned ERROR on write of 49 bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyNameUnderOutputDirectory_FailsInTheOpenAsCurlDoes()
    {
        InMemoryFileSystem directoryFiles = new() { UnwritableStatus = FileAccessStatus.IsDirectory };
        directoryFiles.UnwritablePaths.Add("od/");
        DeferredOutputFileStream output = new(directoryFiles, "od/u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => "od/" + name);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\"\"\r\n")));

        Assert.AreEqual("Warning: Failed to open the file od/: Permission denied", output.OpenFailureWarning);
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_ReadsTheLinesToo()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => name);
        byte[] head = Encoding.ASCII.GetBytes("xHTTP/1.1 200 OK\r\n" + Disposition);

        await stream.WriteAsync(head, 1, head.Length - 1, CancellationToken.None);

        Assert.AreEqual("x.txt", output.Path);
        Assert.AreEqual(head.Length - 1, passedOn.Length);
    }

    [TestMethod]
    public void Write_Synchronous_IsNotSupported()
    {
        RemoteHeaderNameStream stream = new(new DeferredOutputFileStream(files, "u", FileWriteMode.Truncate), null, name => name);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public void StreamMembers_DescribeAWriteOnlyStream()
    {
        RemoteHeaderNameStream stream = new(new DeferredOutputFileStream(files, "u", FileWriteMode.Truncate), null, name => name);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
