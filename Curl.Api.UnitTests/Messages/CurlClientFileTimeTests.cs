using Curl.Api.Info;

namespace Curl.Api.Messages;

[TestClass]
public sealed class CurlClientFileTimeTests
{
    private const string LastModified = "Last-Modified: Fri, 20 Nov 2026 13:31:00 GMT\r\n";

    private const long LastModifiedSeconds = 1795181460;

    private static async Task<long> FileTimeAsync(bool fetchFileTime, string headerLine, long engineFileTime = -1)
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = [1], Info = new TransferInfo { FileTime = engineFileTime } };
        easy.HeaderLines.AddRange(["HTTP/1.1 200 OK\r\n", headerLine, "\r\n"]);
        await using CurlClient client = new(new ScriptedCurlEngine(easy), new CurlClientOptions { FetchFileTime = fetchFileTime }, ownsEngine: true);

        CurlResponse response = await client.SendAsync(new(HttpMethod.Head, "ftp://example/file"), CancellationToken.None);

        return response.Info.FileTime;
    }

    [TestMethod]
    public async Task SendAsync_FetchFileTimeAndLastModified_ReportsItsEpochSeconds() =>
        Assert.AreEqual(LastModifiedSeconds, await FileTimeAsync(true, LastModified));

    [TestMethod]
    public async Task SendAsync_WithoutFetchFileTime_KeepsFileTimeUnknown() =>
        Assert.AreEqual(-1, await FileTimeAsync(false, LastModified));

    [TestMethod]
    public async Task SendAsync_EngineAlreadyKnowsTheFileTime_KeepsIt() =>
        Assert.AreEqual(42, await FileTimeAsync(true, LastModified, 42));

    [TestMethod]
    public async Task SendAsync_NoLastModifiedHeader_KeepsFileTimeUnknown() =>
        Assert.AreEqual(-1, await FileTimeAsync(true, "X-A: b\r\n"));

    [TestMethod]
    public async Task SendAsync_UnparsableLastModified_KeepsFileTimeUnknown() =>
        Assert.AreEqual(-1, await FileTimeAsync(true, "Last-Modified: yesterday\r\n"));
}
