using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RecordReplay.UnitTests;

/// <summary>A <c>-G -d</c> request is a GET: curl moves the data into the query and sends no body (BL-5139).</summary>
[TestClass]
public sealed class ExchangeKeyGetFlagTests
{
    private const string GetUrl = "http://127.0.0.1:28000/echo?a=1";
    private const string Head = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n";

    private string _directory = null!;

    [TestInitialize]
    public void CreatePath() => _directory = Path.Combine(Path.GetTempPath(), "recordreplay-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Record_GetFlagWithData_RecordsAGetLineAndAKeyWithoutTheDataBytes_Bl5139()
    {
        await Hook(record: _directory).ExecuteAsync(GetFlagContext(), Server);

        var file = Directory.GetFiles(_directory).Single();
        Assert.AreEqual("GET", ExchangeKey.MethodOf(GetFlagContext()));
        Assert.AreEqual(ExchangeKey.For("GET", GetUrl, default) + ".exchange", Path.GetFileName(file));
        Assert.AreNotEqual(ExchangeKey.For("GET", GetUrl, "a=1"u8) + ".exchange", Path.GetFileName(file));
        Assert.AreEqual("GET " + GetUrl, Encoding.Latin1.GetString(File.ReadAllBytes(file)).Split('\n')[1]);
    }

    [TestMethod]
    public async Task Replay_OfAGetFlagRecording_ReportsGet_Bl5139()
    {
        await Hook(record: _directory).ExecuteAsync(GetFlagContext(), Server);

        var result = await Hook(replay: _directory).ExecuteAsync(GetFlagContext(), NeverCalled);

        Assert.AreEqual("GET", result.Report!.Method);
    }

    [TestMethod]
    public async Task Replay_DataWithoutTheGetFlag_StillRecordsAndReportsPost_Bl5139()
    {
        await Hook(record: _directory).ExecuteAsync(PostContext(), Server);

        var file = Directory.GetFiles(_directory).Single();
        Assert.AreEqual(ExchangeKey.For("POST", "http://127.0.0.1:28000/echo", "a=1"u8) + ".exchange", Path.GetFileName(file));
        var result = await Hook(replay: _directory).ExecuteAsync(PostContext(), NeverCalled);
        Assert.AreEqual("POST", result.Report!.Method);
    }

    private static ValueTask<TransferResult> Server(ITransferContext context) => ServeAsync(context);

    private static async ValueTask<TransferResult> ServeAsync(ITransferContext context)
    {
        await context.HeaderOutput!.WriteAsync(Encoding.Latin1.GetBytes(Head));
        await context.Output.WriteAsync("ok"u8.ToArray());
        return TransferResult.Success(2) with { Report = new TransferReport { ResponseCode = 200 } };
    }

    private static ValueTask<TransferResult> NeverCalled(ITransferContext context) => throw new AssertFailedException("The server was contacted.");

    private static ITransferHook Hook(string? replay = null, string? record = null)
    {
        var values = new Dictionary<string, string>();
        if (replay is not null)
        {
            values[RecordReplayExtension.ReplayOption.Name] = replay;
        }

        if (record is not null)
        {
            values[RecordReplayExtension.RecordOption.Name] = record;
        }

        return RecordReplayExtension.HookFor(new(values))!;
    }

    // The console hands the transfer the -G URL with the query already added and no PostData.
    private static TransferContext GetFlagContext() => Context(GetUrl, postData: null);

    private static TransferContext PostContext() => Context("http://127.0.0.1:28000/echo", "a=1"u8.ToArray());

    private static TransferContext Context(string url, ReadOnlyMemory<byte>? postData) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        DumpHeaderOutput = new MemoryStream(),
        PostData = postData,
        Http = new HttpRequestOptions(),
    };
}
