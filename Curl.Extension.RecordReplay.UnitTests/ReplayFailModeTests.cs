using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay.UnitTests;

[TestClass]
public sealed class ReplayFailModeTests
{
    [TestMethod]
    public async Task ExecuteAsync_ReplayOf404UnderFail_Exits22WithoutBodyAndReports404_Adr1135Section5()
    {
        var (result, body, head, dump) = await ReplayAsync(404, HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
        Assert.AreEqual(string.Empty, body);
        Assert.AreEqual(string.Empty, head);
        Assert.AreNotEqual(string.Empty, dump);
        Assert.AreEqual(404, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayOf404UnderFailWithBody_WritesBodyAndExits22_Adr1135Section5()
    {
        var (result, body, head, _) = await ReplayAsync(404, HttpFailMode.FailWithBody);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
        Assert.AreEqual("notfound\n", body);
        Assert.AreNotEqual(string.Empty, head);
        Assert.AreEqual(404, result.Report!.ResponseCode);
    }

    [TestMethod]
    [DataRow(200)]
    [DataRow(302)]
    [DataRow(404)]
    public async Task ExecuteAsync_ReplayThatIsNotAFailure_SucceedsWithBody_Adr1135Section5(int status)
    {
        var (result, body, _, _) = await ReplayAsync(status, status == 404 ? HttpFailMode.None : HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("notfound\n", body);
        Assert.AreEqual(status, result.Report!.ResponseCode);
    }

    private static async Task<(TransferResult Result, string Body, string Head, string Dump)> ReplayAsync(int status, HttpFailMode mode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "recordreplay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var record = RecordReplayExtension.HookFor(new(new Dictionary<string, string> { [RecordReplayExtension.RecordOption.Name] = directory }))!;
            await record.ExecuteAsync(Context(HttpFailMode.None), async context =>
            {
                await context.HeaderOutput!.WriteAsync(Encoding.Latin1.GetBytes($"HTTP/1.1 {status} X\r\n\r\n"));
                await context.Output.WriteAsync(Encoding.Latin1.GetBytes("notfound\n"));
                return TransferResult.Success(9) with { Report = new TransferReport { ResponseCode = status } };
            });
            var replay = RecordReplayExtension.HookFor(new(new Dictionary<string, string> { [RecordReplayExtension.ReplayOption.Name] = directory }))!;
            var context = Context(mode);
            var result = await replay.ExecuteAsync(context, _ => throw new AssertFailedException("The server was contacted."));
            return (result, Text(context.Output), Text(context.HeaderOutput!), Text(context.DumpHeaderOutput!));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static TransferContext Context(HttpFailMode mode) => new()
    {
        Url = CurlUrl.Parse("http://example.com/404"),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        Http = new HttpRequestOptions { Fail = mode },
    };
}
