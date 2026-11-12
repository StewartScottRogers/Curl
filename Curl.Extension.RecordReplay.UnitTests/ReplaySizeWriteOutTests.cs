using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class ReplaySizeWriteOutTests
{
    private const string Head = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Replay_ReportsTheRecordedBodyAndHeadSizes_Adr1135Section5()
    {
        var directory = Path.Combine(Path.GetTempPath(), "recordreplay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var record = RecordReplayExtension.HookFor(new(new Dictionary<string, string> { [RecordReplayExtension.RecordOption.Name] = directory }))!;
            await record.ExecuteAsync(Context(), async context =>
            {
                await context.HeaderOutput!.WriteAsync(Encoding.Latin1.GetBytes(Head));
                await context.Output.WriteAsync(Encoding.Latin1.GetBytes("eighteen bytes....")); // 18 bytes
                return TransferResult.Success(18) with { Report = new TransferReport { ResponseCode = 200 } };
            });
            var replay = RecordReplayExtension.HookFor(new(new Dictionary<string, string> { [RecordReplayExtension.ReplayOption.Name] = directory }))!;

            var result = await replay.ExecuteAsync(Context(), _ => throw new AssertFailedException("The server was contacted."));

            Assert.AreEqual(18L, result.Report!.DownloadSize);
            Assert.AreEqual((long)Head.Length, result.Report.HeaderSize);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("http://example.com/r"),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = new HttpRequestOptions(),
    };
}
