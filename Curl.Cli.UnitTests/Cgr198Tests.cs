using System.Text;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-198 (BL-3727) for the <c>-F</c> body: on Windows <c>-F f=@sub</c> naming a directory sends the POST head and the
/// part's headers, and then fails the read with exit 26, <c>read error getting mime data</c>, where the body used to fail
/// before anything was sent (measured with curl 8.22.0 on Windows).
/// </summary>
[TestClass]
public sealed class Cgr198Tests
{
    private const string Boundary = "------------------------AvBMzYwWK0xAwxw4mh26IQ";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Build_EncodedDirectoryPartWhoseReadFailsAtSend_SendsTheEncodingHeaderThenFailsTheRead()
    {
        string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cgr198-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Diagnostics.Arrange("arguments", "-F \"f=@<directory>;encoder=base64\", Windows build, read fails at send");
            MultipartFormBodyBuilder builder = new(new PhysicalFileSystem(), Encoding.UTF8, () => Boundary)
            {
                SendsDirectoryEmpty = false,
                DirectoryReadFailsAtSend = true,
            };
            MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, directory, null, "sub", [], []) { Encoder = "base64" };

            MultipartFormBuildResult result = await builder.BuildAsync([part], TestContext.CancellationToken);

            Assert.IsTrue(result.IsBuilt);
            byte[] buffer = new byte[4096];
            int head = await result.Body.Content.ReadAsync(buffer, TestContext.CancellationToken);
            string sent = Encoding.UTF8.GetString(buffer, 0, head);
            Diagnostics.Assert("part head", "headers with Content-Transfer-Encoding: base64", sent);
            Assert.Contains("Content-Transfer-Encoding: base64\r\n\r\n", sent);
            await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(
                async () => await result.Body.Content.ReadExactlyAsync(buffer, TestContext.CancellationToken));
            await result.Body.Content.DisposeAsync();
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task Build_DirectoryPartWhoseReadFailsAtSend_SendsThePartHeadThenFailsTheRead()
    {
        string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cgr198-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Diagnostics.Arrange("arguments", "-F f=@<directory>, Windows build, read fails at send");
            MultipartFormBodyBuilder builder = new(new PhysicalFileSystem(), Encoding.UTF8, () => Boundary)
            {
                SendsDirectoryEmpty = false,
                DirectoryReadFailsAtSend = true,
            };
            MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, directory, null, "sub", [], []);

            MultipartFormBuildResult result = await builder.BuildAsync([part], TestContext.CancellationToken);

            Diagnostics.Assert("built", true, result.IsBuilt);
            Assert.IsTrue(result.IsBuilt);
            Assert.IsNull(result.Body.Length, "The directory's size is unknown, so the body is sent chunked.");
            byte[] buffer = new byte[4096];
            int head = await result.Body.Content.ReadAsync(buffer, TestContext.CancellationToken);
            string sent = Encoding.UTF8.GetString(buffer, 0, head);
            string expected = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"sub\"\r\nContent-Type: application/octet-stream\r\n\r\n";
            Diagnostics.Assert("part head", expected, sent);
            Assert.AreEqual(expected, sent);
            RequestBodyReadFailedException failure = await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(
                async () => await result.Body.Content.ReadExactlyAsync(buffer, TestContext.CancellationToken));
            Diagnostics.Assert("message", MultipartFormBodyBuilder.ReadFailedMessage, failure.Message);
            Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, failure.Message);
            Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
            await result.Body.Content.DisposeAsync();
        }
        finally
        {
            Directory.Delete(directory);
        }
    }
}
