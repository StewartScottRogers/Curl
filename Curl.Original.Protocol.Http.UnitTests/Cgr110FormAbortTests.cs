using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestBodyWriter" /> to the exit code a
/// <see cref="RequestBodyReadFailedException" /> carries: curl 8.22.0's <c>-F f=@-</c> with
/// standard input closed sends the part head chunked and ends with exit 42,
/// <c>operation aborted by callback</c>, not exit 26 (CGR-110, BL-3985, ADR-0967).
/// </summary>
[TestClass]
public sealed class Cgr110FormAbortTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_ChunkedBodyReadIsAborted_SendsTheChunksBeforeItThenFailsWithExit42()
    {
        FailingReadStream stream = new("head"u8.ToArray(), 4, new RequestBodyReadFailedException(CurlExitCode.AbortedByCallback, "operation aborted by callback"));
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, null, "multipart/form-data; boundary=b"), true, TestContext.CancellationToken));

        TestDiagnostics.For(TestContext).Assert(
            "exit code, message, written",
            "AbortedByCallback, operation aborted by callback, 4\r\nhead\r\n",
            $"{failure.ExitCode}, {failure.Message}, {Encoding.Latin1.GetString(connection.Written)}");
        Assert.AreEqual(CurlExitCode.AbortedByCallback, failure.ExitCode);
        Assert.AreEqual("operation aborted by callback", failure.Message);
        Assert.AreEqual("4\r\nhead\r\n", Encoding.Latin1.GetString(connection.Written));
    }
}
