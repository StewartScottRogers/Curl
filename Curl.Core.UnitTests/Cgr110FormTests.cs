using System.Text;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins the <c>-F f=@-</c> body <see cref="MultipartFormBodyBuilder" /> builds when standard input
/// is a <see cref="ClosedStandardInputStream" />, against curl 8.22.0 (Schannel) measured on
/// 2026-10-28 with <c>curl -sS -F f=@- http://127.0.0.1:18974/ &lt;&amp;-</c>: the body is sent
/// chunked, its first chunk holds the part's head, and the read after it aborts the transfer with
/// exit 42, <c>operation aborted by callback</c> (CGR-110, BL-3985, ADR-0964).
/// </summary>
[TestClass]
public sealed class Cgr110FormTests
{
    private const string Boundary = "------------------------Q7DWhE4bjdusOhY9dHZ5ul";

    private const string PartHead = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"-\"\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task BuildAsync_ClosedStandardInput_SendsThePartHeadChunkedThenAbortsWithExit42()
    {
        StreamBody body = await BuildAsync();
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        byte[] buffer = new byte[65536];
        int read = await body.Content.ReadAsync(buffer, TestContext.CancellationToken);
        RequestBodyReadFailedException failure = await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(
            async () => _ = await body.Content.ReadAsync(buffer, TestContext.CancellationToken));

        diagnostics.Assert("length", null, body.Length);
        diagnostics.Diff("first read", PartHead, Encoding.Latin1.GetString(buffer, 0, read));
        diagnostics.Assert("exit code, message", "AbortedByCallback, operation aborted by callback", $"{failure.ExitCode}, {failure.Message}");
        Assert.IsNull(body.Length);
        Assert.AreEqual(PartHead, Encoding.Latin1.GetString(buffer, 0, read));
        Assert.AreEqual(CurlExitCode.AbortedByCallback, failure.ExitCode);
        Assert.AreEqual("operation aborted by callback", failure.Message);
    }

    [TestMethod]
    public async Task BuildAsync_ClosedStandardInputReadSynchronously_AbortsWithExit42AfterThePartHead()
    {
        StreamBody body = await BuildAsync();

        byte[] buffer = new byte[65536];
        int read = body.Content.Read(buffer, 0, buffer.Length);
        RequestBodyReadFailedException failure = Assert.ThrowsExactly<RequestBodyReadFailedException>(
            () => _ = body.Content.Read(buffer, 0, buffer.Length));

        TestDiagnostics.For(TestContext).Assert("first read, exit code", $"{PartHead}, AbortedByCallback", $"{Encoding.Latin1.GetString(buffer, 0, read)}, {failure.ExitCode}");
        Assert.AreEqual(PartHead, Encoding.Latin1.GetString(buffer, 0, read));
        Assert.AreEqual(CurlExitCode.AbortedByCallback, failure.ExitCode);
    }

    private async Task<StreamBody> BuildAsync()
    {
        using ClosedStandardInputStream standardInput = new();
        MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, MultipartFormPart.StandardInputPath, null, null, [], []);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, () => Boundary, standardInput);
        MultipartFormBuildResult built = await builder.BuildAsync([part], TestContext.CancellationToken);
        TestDiagnostics.For(TestContext).Act("isBuilt", built.IsBuilt);
        Assert.IsTrue(built.IsBuilt);
        return built.Body;
    }
}
