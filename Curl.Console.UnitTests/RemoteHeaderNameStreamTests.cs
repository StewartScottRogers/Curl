using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_NameInOkResponse_OpensTheNamedFileAndPassesTheLinesOn()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => "od/" + name);
        string head = "HTTP/1.1 200 OK\r\n" + Disposition + "\r\n";
        Diagnostics.Arrange("url file name / output directory", "u.txt / od");
        Diagnostics.Arrange("header", head.ReplaceLineEndings("\n"));

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        ActOutput(output);
        Diagnostics.Bytes("passed on", passedOn.ToArray());

        Diagnostics.Assert("output path / open", "od/x.txt / True", $"{output.Path} / {output.IsOpen}");
        Diagnostics.Diff("passed on", head, Encoding.ASCII.GetString(passedOn.ToArray()));
        Assert.AreEqual("od/x.txt", output.Path);
        Assert.IsTrue(output.IsOpen);
        Assert.AreEqual(head, Encoding.ASCII.GetString(passedOn.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_SecondName_IsIgnored()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);
        Diagnostics.Arrange("names in order", "x.txt, then y.txt");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("Content-Disposition: attachment; filename=y.txt\r\n"));
        ActOutput(output);

        Diagnostics.Assert("output path", "x.txt", output.Path);
        Assert.AreEqual("x.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_StatusLineWithoutCode_IgnoresTheName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);
        Diagnostics.Arrange("status lines", "'HTTP/1.1', 'HTTP/1.1 ', 'HTTP/1.1 xyz', 'HTTP/1.1 ' split from its name");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 \r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 xyz\r\n" + Disposition));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 "));
        await stream.WriteAsync(Encoding.ASCII.GetBytes(Disposition));
        ActOutput(output);

        Diagnostics.Assert("output path / open", "u.txt / False", $"{output.Path} / {output.IsOpen}");
        Assert.AreEqual("u.txt", output.Path);
        Assert.IsFalse(output.IsOpen);
    }

    [TestMethod]
    public async Task WriteAsync_NoStatusLine_IgnoresTheName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);
        Diagnostics.Arrange("header", Disposition.ReplaceLineEndings("\n"));

        await stream.WriteAsync(Encoding.ASCII.GetBytes(Disposition));
        ActOutput(output);

        Diagnostics.Assert("output path", "u.txt", output.Path);
        Assert.AreEqual("u.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_NameTaken_ThrowsIOException()
    {
        files.ExistingPaths.Add("x.txt");
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => name);
        Diagnostics.Arrange("existing files", "x.txt");

        IOException exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition)));
        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Act("open failure warning", output.OpenFailureWarning);
        Diagnostics.Act("passed on length", passedOn.Length);

        Diagnostics.Assert("open failure warning", "Warning: Failed to open the file x.txt: File exists", output.OpenFailureWarning);
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
        Diagnostics.Arrange("file system", "creates the chosen file just before the exclusive open");

        await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + Disposition)));
        Diagnostics.Act("chosen", chosen);
        Diagnostics.Act("write modes", string.Join(",", files.WriteModes));
        Diagnostics.Act("open failure warning", output.OpenFailureWarning);

        Diagnostics.Assert("chosen", "x.txt", chosen);
        Assert.AreEqual("x.txt", chosen);
        CollectionAssert.AreEqual(new[] { FileWriteMode.CreateNew }, files.WriteModes);
        Assert.AreEqual(0, files.Written.Count);
        Assert.AreEqual("Warning: Failed to open the file x.txt: File exists", output.OpenFailureWarning);
        TransferResult result = await output.CompleteAsync(TransferResult.Success(0));
        Diagnostics.Act("completed exit code / error", $"{(int)result.ExitCode} / {result.ErrorMessage}");
        Diagnostics.Assert("completed exit code", CurlExitCode.WriteError, result.ExitCode);
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
        Diagnostics.Arrange("output directory / remote name", "od / empty");

        await Assert.ThrowsExactlyAsync<IOException>(
            async () => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\"\"\r\n")));
        Diagnostics.Act("open failure warning", output.OpenFailureWarning);

        Diagnostics.Assert("open failure warning", "Warning: Failed to open the file od/: Permission denied", output.OpenFailureWarning);
        Assert.AreEqual("Warning: Failed to open the file od/: Permission denied", output.OpenFailureWarning);
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_ReadsTheLinesToo()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, passedOn, name => name);
        byte[] head = Encoding.ASCII.GetBytes("xHTTP/1.1 200 OK\r\n" + Disposition);
        Diagnostics.Arrange("buffer offset / count", $"1 / {head.Length - 1}");
        Diagnostics.Bytes("buffer", head);

        await stream.WriteAsync(head, 1, head.Length - 1, CancellationToken.None);
        ActOutput(output);
        Diagnostics.Act("passed on length", passedOn.Length);

        Diagnostics.Assert("output path / passed on length", $"x.txt / {head.Length - 1}", $"{output.Path} / {passedOn.Length}");
        Assert.AreEqual("x.txt", output.Path);
        Assert.AreEqual(head.Length - 1, passedOn.Length);
    }

    [TestMethod]
    public void Write_Synchronous_IsNotSupported()
    {
        RemoteHeaderNameStream stream = new(new DeferredOutputFileStream(files, "u", FileWriteMode.Truncate), null, name => name);
        Diagnostics.Arrange("call", "Write(byte[1], 0, 1)");

        NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("exception", nameof(NotSupportedException), exception.GetType().Name);
    }

    [TestMethod]
    public void StreamMembers_DescribeAWriteOnlyStream()
    {
        RemoteHeaderNameStream stream = new(new DeferredOutputFileStream(files, "u", FileWriteMode.Truncate), null, name => name);
        Diagnostics.Arrange("stream", "fresh, nothing written");
        Diagnostics.Act("can read / seek / write", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");

        Diagnostics.Assert("can read / seek / write", "False / False / True", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");
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

    [TestMethod]
    public async Task WriteAsync_FollowedLocationsWithoutName_NameTheFileAfterTheLastLocation()
    {
        DeferredOutputFileStream output = new(files, "1643", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => "od/" + name, followsRedirects: true);
        Diagnostics.Arrange("responses", "301 to /16430002, 302 to /16430003?x=1, 200 with no Content-Disposition");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 301 Moved\r\nLocation: /16430002\r\n\r\n"));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nlocation: /16430003?x=1\r\n\r\n"));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nLocation: /ignored\r\n\r\n"));
        ActOutput(output);

        Diagnostics.Assert("output path / open", "od/16430003 / False", $"{output.Path} / {output.IsOpen}");
        Assert.AreEqual("od/16430003", output.Path);
        Assert.IsFalse(output.IsOpen);
    }

    [TestMethod]
    public async Task WriteAsync_LocationAfterName_KeepsTheName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name, followsRedirects: true);
        Diagnostics.Arrange("response", "302 naming x.txt, then Location: /other");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\n" + Disposition + "Location: /other\r\n"));
        ActOutput(output);

        Diagnostics.Assert("output path", "x.txt", output.Path);
        Assert.AreEqual("x.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_LocationNotFollowed_KeepsTheUrlName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name);
        Diagnostics.Arrange("response, no -L", "302 to /other");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: /other\r\n"));
        ActOutput(output);

        Diagnostics.Assert("output path", "u.txt", output.Path);
        Assert.AreEqual("u.txt", output.Path);
    }

    [TestMethod]
    public async Task WriteAsync_LocationWithoutFileName_KeepsTheUrlName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name, followsRedirects: true);
        Diagnostics.Arrange("response", "302 to http://host and a 302 header that is not Location");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nServer: x\r\nLocation: http://host\r\n"));
        ActOutput(output);

        Diagnostics.Assert("output path", "u.txt", output.Path);
        Assert.AreEqual("u.txt", output.Path);
    }

    [TestMethod]
    public async Task RenameBeforeOpen_OpenFile_KeepsItsName()
    {
        DeferredOutputFileStream output = new(files, "u.txt", FileWriteMode.Truncate);
        await output.WriteAsync(Encoding.ASCII.GetBytes("body"));
        Diagnostics.Arrange("file", "u.txt, already open");

        output.RenameBeforeOpen("v.txt");
        ActOutput(output);

        Diagnostics.Assert("output path", "u.txt", output.Path);
        Assert.AreEqual("u.txt", output.Path);
    }

    private void ActOutput(DeferredOutputFileStream output)
    {
        Diagnostics.Act("output path", output.Path);
        Diagnostics.Act("output open", output.IsOpen);
    }
}
