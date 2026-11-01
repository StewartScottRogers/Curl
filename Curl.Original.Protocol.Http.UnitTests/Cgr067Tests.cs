using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-067 (BL-3596): a final 2xx that arrives while the request
/// waits for <c>100 Continue</c> does not stop the body. curl 8.22.0 (mingw, Schannel) still
/// waits out its one second, prints <c>Done waiting for 100-continue</c> and sends the body,
/// measured on 2026-10-26 with <c>Record-CurlExchange.ps1 -RespondAfterBodyBytes 0</c>
/// (BL-3596 Notes).
/// </summary>
[TestClass]
public sealed class Cgr067Tests
{
    private const string EarlyOk = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(1)]
    [DataRow(65536)]
    public async Task WaitForContinueAsync_FinalSuccessArrivesFirst_WaitsTheSecondOutAndSendsTheBody(int chunkSize)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(EarlyOk), chunkSize));
        Diagnostics.Arrange("response, chunk size", $"{EarlyOk.Replace("\r", "\\r").Replace("\n", "\\n")}, {chunkSize}");

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(TimeSpan.FromMilliseconds(999));
        Diagnostics.Act("completed at 999 ms", wait.IsCompleted);
        Assert.IsFalse(wait.IsCompleted, "The wait ended before one second.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        bool sends = await wait;

        Diagnostics.Assert("sends the body, wait ran out", "True, True", $"{sends}, {connection.WaitRanOut}");
        Assert.IsTrue(sends);
        Assert.IsTrue(connection.WaitRanOut);
        Assert.IsFalse(connection.StopsSending);
        Assert.AreEqual(EarlyOk, await ReadAllAsync(connection));
    }

    private static async Task<string> ReadAllAsync(HttpContinueWaitConnection connection)
    {
        MemoryStream all = new();
        byte[] buffer = new byte[7];
        int read;
        while ((read = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            all.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(all.ToArray());
    }
}
