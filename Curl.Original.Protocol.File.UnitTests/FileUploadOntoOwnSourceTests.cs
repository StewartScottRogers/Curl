using System.Text;
using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins <c>-T s.txt file:///dir/s.txt</c>: the destination is the source, truncating it
/// empties the source, and curl 8.22.0 fails with exit 26 where it must have read all six
/// bytes (BL-5017). The tool appends the name to a URL ending in <c>/</c> before the handler
/// sees it, so that case reaches the handler as the same URL.
/// </summary>
[TestClass]
public sealed class FileUploadOntoOwnSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_DestinationIsTheSource_FailsWithReadErrorZeroOfSix()
    {
        var result = await UploadAsync("file:///dir/s.txt", new EmptiedWhenSizedStream(6));

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("message", "client read function EOF fail, only 0/6 of needed bytes read", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(26, (int)result.ExitCode);
        Assert.AreEqual("client read function EOF fail, only 0/6 of needed bytes read", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NameAppendedToDirectoryUrlIsTheSource_FailsWithReadErrorZeroOfSix()
    {
        var result = await UploadAsync("file:///dir/s.txt", new EmptiedWhenSizedStream(6));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("client read function EOF fail, only 0/6 of needed bytes read", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DifferentDestination_CopiesTheSource()
    {
        var fileSystem = new FakeFileSystem();

        var result = await UploadAsync("file:///dir/d.txt", new MemoryStream(Encoding.ASCII.GetBytes("HELLO\n")), fileSystem);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HELLO\n", Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath("/dir/d.txt"))));
    }

    [TestMethod]
    public async Task ExecuteAsync_ExistingDifferentDestination_TruncatesAndReplacesIt()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath("/dir/d.txt"), Encoding.ASCII.GetBytes("OLD CONTENT LONGER"));

        var result = await UploadAsync("file:///dir/d.txt", new MemoryStream(Encoding.ASCII.GetBytes("HELLO\n")), fileSystem);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HELLO\n", Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath("/dir/d.txt"))));
    }

    private static string OsPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static async Task<TransferResult> UploadAsync(string url, Stream upload, FakeFileSystem? fileSystem = null)
    {
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = CurlUrl.Parse(url),
            Upload = upload,
        };

        return await new FileProtocolHandler(fileSystem ?? new FakeFileSystem()).ExecuteAsync(context);
    }

    /// <summary>
    /// A seekable source whose length is its real one when first asked and zero afterwards,
    /// as a file is once the upload truncates it by opening itself as the destination.
    /// </summary>
    private sealed class EmptiedWhenSizedStream(long length) : Stream
    {
        private bool sized;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                long answer = sized ? 0 : length;
                sized = true;
                return answer;
            }
        }

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => Position = offset;

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
