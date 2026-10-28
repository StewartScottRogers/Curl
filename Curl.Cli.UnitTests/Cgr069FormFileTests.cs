using System.Text;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-069 part 3 (BL-3896) for the <c>-F</c> body: <c>-F f=@sub</c> naming a directory is sent by curl's
/// Linux and macOS builds as a part with <c>filename="sub"</c> and no data, the body chunked, and curl exits 0
/// (measured with curl 8.18.0 on Linux on 2026-10-28); curl's Windows build fails with exit 26,
/// <c>read error getting mime data</c>.
/// </summary>
[TestClass]
public sealed class Cgr069FormFileTests
{
    private const string Boundary = "------------------------AvBMzYwWK0xAwxw4mh26IQ";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Build_FilePartNamingADirectoryOffWindows_SendsAnEmptyPartChunked()
    {
        string directory = CreateDirectory();
        try
        {
            Diagnostics.Arrange("arguments", "-F f=@<directory>, Linux build");
            MultipartFormBuildResult result = await BuildAsync(directory, sendsDirectoryEmpty: true);

            Diagnostics.Assert("built", true, result.IsBuilt);
            Assert.IsTrue(result.IsBuilt);
            Diagnostics.Assert("length", null, result.Body.Length);
            Assert.IsNull(result.Body.Length, "A directory's size is unknown, so the body is sent chunked.");
            using MemoryStream copy = new();
            await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
            await result.Body.Content.DisposeAsync();
            string expected =
                $"--{Boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"sub\"\r\n"
                + $"Content-Type: application/octet-stream\r\n\r\n\r\n--{Boundary}--\r\n";
            string body = Encoding.UTF8.GetString(copy.ToArray());
            Diagnostics.Assert("body", expected, body);
            Assert.AreEqual(expected, body);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task Build_EncodedFilePartNamingADirectoryOffWindows_SendsAnEmptyEncodedPart()
    {
        string directory = CreateDirectory();
        try
        {
            Diagnostics.Arrange("arguments", "-F \"f=@<directory>;encoder=base64\", Linux build");
            MultipartFormBuildResult result = await BuildAsync(directory, sendsDirectoryEmpty: true, encoder: "base64");

            Diagnostics.Assert("built", true, result.IsBuilt);
            Assert.IsTrue(result.IsBuilt);
            using MemoryStream copy = new();
            await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
            await result.Body.Content.DisposeAsync();
            string body = Encoding.UTF8.GetString(copy.ToArray());
            Diagnostics.Assert("body", "headers with Content-Transfer-Encoding: base64, no data", body);
            Assert.EndsWith($"Content-Transfer-Encoding: base64\r\n\r\n\r\n--{Boundary}--\r\n", body);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task Build_FilePartNamingAMissingFileOffWindows_StillFailsWithOpenError()
    {
        string missing = Path.Combine(Path.GetTempPath(), "cgr069-" + Guid.NewGuid().ToString("N"));
        Diagnostics.Arrange("arguments", "-F f=@<missing file>, Linux build");

        MultipartFormBuildResult result = await BuildAsync(missing, sendsDirectoryEmpty: true);

        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Failure?.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(MultipartFormBodyBuilder.OpenFailedMessage, result.Failure.ErrorMessage);
    }

    [TestMethod]
    public async Task Build_FilePartNamingADirectoryOnWindows_FailsWithReadError()
    {
        string directory = CreateDirectory();
        try
        {
            Diagnostics.Arrange("arguments", "-F f=@<directory>, Windows build");
            MultipartFormBuildResult result = await BuildAsync(directory, sendsDirectoryEmpty: false);

            Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Failure?.ExitCode);
            Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
            Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    private static string CreateDirectory() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cgr069-" + Guid.NewGuid().ToString("N"))).FullName;

    private async Task<MultipartFormBuildResult> BuildAsync(string path, bool sendsDirectoryEmpty, string? encoder = null)
    {
        MultipartFormBodyBuilder builder = new(new PhysicalFileSystem(), Encoding.UTF8, () => Boundary)
        {
            SendsDirectoryEmpty = sendsDirectoryEmpty,
        };
        MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, path, null, "sub", [], []) { Encoder = encoder };
        return await builder.BuildAsync([part], TestContext.CancellationToken);
    }
}
