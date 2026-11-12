using System.Text;
using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins <c>-T src -C - file:///dst</c>: the offset is the destination's size and the rest of
/// the source is appended, as curl 8.22.0's <c>file_upload</c> does (BL-4902).
/// </summary>
[TestClass]
public sealed class FileUploadResumeUnknownOffsetTests
{
    private static readonly byte[] Source = Encoding.ASCII.GetBytes("UPLOAD\n");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/dst");

    private static string OsPath => "/dir/dst".Replace('/', Path.DirectorySeparatorChar);

    [TestMethod]
    public async Task ExecuteAsync_DestinationShorterThanSource_AppendsTheSourceFromTheDestinationSize()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("ZZ"));

        var result = await UploadResumingFromUnknownOffsetAsync(fileSystem, resumeFrom: null);

        string written = Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath));
        Diagnostics.Arrange("destination", "ZZ");
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("destination", "ZZLOAD\n", written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ZZLOAD\n", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_DestinationLongerThanSource_LeavesTheDestinationUnchanged()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("ZZZZZZZZZZZ"));

        var result = await UploadResumingFromUnknownOffsetAsync(fileSystem, resumeFrom: null);

        string written = Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath));
        Diagnostics.Arrange("destination", "ZZZZZZZZZZZ");
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("destination", "ZZZZZZZZZZZ", written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ZZZZZZZZZZZ", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_DestinationMissing_WritesTheWholeSource()
    {
        var fileSystem = new FakeFileSystem();

        var result = await UploadResumingFromUnknownOffsetAsync(fileSystem, resumeFrom: null);

        string written = Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath));
        Diagnostics.Arrange("destination", "missing");
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("destination", "UPLOAD\n", written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("UPLOAD\n", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExplicitOffsetBesideTheFlag_SkipsTheExplicitOffset()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("ZZ"));

        var result = await UploadResumingFromUnknownOffsetAsync(fileSystem, resumeFrom: 4);

        string written = Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath));
        Diagnostics.Arrange("destination, resume from", "ZZ, 4");
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("destination", "ZZAD\n", written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ZZAD\n", written);
    }

    private static async Task<TransferResult> UploadResumingFromUnknownOffsetAsync(
        FakeFileSystem fileSystem,
        long? resumeFrom)
    {
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = FileUrl,
            Upload = new MemoryStream(Source),
            ResumeFrom = resumeFrom,
            ResumeUploadFromUnknownOffset = true,
        };

        return await new FileProtocolHandler(fileSystem).ExecuteAsync(context);
    }
}
