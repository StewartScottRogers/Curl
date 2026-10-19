using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Messages;
using Curl.Api.Progress;
using Curl.Api.WebSockets;

namespace Curl.Api.Readme;

[TestClass]
public sealed partial class ReadmeTests
{
    private static readonly string[] SampleNames =
    [
        "usings", "create", "send", "options", "upload", "cancel", "progress", "multipart", "websocket", "concurrent", "httpclient",
    ];

    [GeneratedRegex(@"<!-- sample: (\w+) -->\r?\n```csharp\r?\n(.*?)\r?\n```", RegexOptions.Singleline)]
    private static partial Regex ReadmeBlock();

    private static string EmbeddedText(string name)
    {
        using Stream stream = typeof(ReadmeTests).Assembly.GetManifestResourceStream(name)!;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    private static Dictionary<string, string> ReadmeBlocks() =>
        ReadmeBlock().Matches(EmbeddedText("README.md")).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

    private static string SourceSample(string name)
    {
        string[] lines = EmbeddedText("ReadmeSamples.cs").Split('\n');
        int start = Array.FindIndex(lines, line => line.Trim() == $"// <sample {name}>");
        int end = Array.FindIndex(lines, start + 1, line => line.Trim() == "// </sample>");
        string[] body = lines[(start + 1)..end];
        int indent = body.Where(line => line.Length > 0).Min(line => line.Length - line.TrimStart().Length);
        return string.Join('\n', body.Select(line => line.Length == 0 ? line : line[indent..]));
    }

    private static ScriptedEasyTransfer Scripted()
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = "hello"u8.ToArray(), Info = new TransferInfo { ResponseCode = 200 } };
        easy.HeaderLines.AddRange(["HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "\r\n"]);
        return easy;
    }

    private static CurlClient ClientOver(Func<IEasyTransfer> createEasy) => new(new ReadmeEngine(createEasy));

    [TestMethod]
    public void Readme_EverySampleHasABlockIdenticalToTheMarkedSource()
    {
        Dictionary<string, string> blocks = ReadmeBlocks();

        CollectionAssert.AreEquivalent(SampleNames, blocks.Keys.ToArray());
        foreach (string name in SampleNames)
        {
            Assert.AreEqual(SourceSample(name), blocks[name], $"sample '{name}'");
        }
    }

    [TestMethod]
    public void Readme_NamesEveryPublicType()
    {
        string readme = EmbeddedText("README.md");

        string[] missing = [.. typeof(CurlClient).Assembly.GetExportedTypes()
            .Select(type => type.Name)
            .Where(name => !readme.Contains($"`{name}`", StringComparison.Ordinal))];

        CollectionAssert.AreEqual(Array.Empty<string>(), missing);
    }

    [TestMethod]
    public async Task Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything()
    {
        await ReadmeSamples.CreateClientAsync();
    }

    [TestMethod]
    public async Task Sample_Send_StreamsTheBodyToTheDestination()
    {
        await using CurlClient client = ClientOver(Scripted);
        using MemoryStream destination = new();

        await ReadmeSamples.SendAndStreamAsync(client, destination, CancellationToken.None);

        Assert.AreEqual("hello", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [TestMethod]
    public async Task Sample_Options_SetsHeadersFollowAndNetworkOptions()
    {
        ScriptedEasyTransfer easy = Scripted();
        await using CurlClient client = ClientOver(() => easy);

        await ReadmeSamples.SetOptionsAsync(client, CancellationToken.None);

        Assert.AreEqual(5, easy.PerformedOptions!.Http.MaxRedirects);
        CollectionAssert.AreEqual(new[] { "Accept: application/json", "X-Trace: 42" }, easy.PerformedOptions.Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public async Task Sample_Upload_SendsBothBodies()
    {
        List<ScriptedEasyTransfer> handles = [];
        await using CurlClient client = ClientOver(() =>
        {
            ScriptedEasyTransfer easy = Scripted();
            handles.Add(easy);
            return easy;
        });
        using MemoryStream file = new([1, 2, 3]);

        await ReadmeSamples.UploadBytesAndStreamAsync(client, file, CancellationToken.None);

        Assert.HasCount(2, handles);
        Assert.AreEqual("https://example.com/items", handles[0].PerformedOptions!.Network.Url);
        Assert.AreEqual("https://example.com/upload", handles[1].PerformedOptions!.Network.Url);
    }

    [TestMethod]
    [DoNotParallelize] // Console.SetOut is process-wide; another sample's lines must not land in this writer.
    public async Task Sample_Cancel_SucceedsWhenNothingFails()
    {
        ScriptedEasyTransfer easy = Scripted();
        await using CurlClient client = ClientOver(() => easy);
        TextWriter console = Console.Out;
        using StringWriter written = new();
        Console.SetOut(written);
        try
        {
            await ReadmeSamples.CancelAndHandleErrorsAsync(client, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(console);
        }

        Assert.AreEqual("", written.ToString(), "the sample wrote a cancellation or curl error line");
        Assert.AreEqual("https://example.com/slow", easy.PerformedOptions!.Network.Url);
        Assert.AreEqual(5L, easy.BodyBytesTaken);
        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    [DoNotParallelize] // Console.SetOut is process-wide; another sample's lines must not land in this writer.
    public async Task Sample_Cancel_CancelledToken_IsCaughtAsCancellation()
    {
        await using CurlClient client = ClientOver(Scripted);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        TextWriter console = Console.Out;
        using StringWriter written = new();
        Console.SetOut(written);
        try
        {
            await ReadmeSamples.CancelAndHandleErrorsAsync(client, cancelled.Token);
        }
        finally
        {
            Console.SetOut(console);
        }

        Assert.AreEqual("Cancelled: the transfer was aborted.\n", written.ToString().ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public async Task Sample_Cancel_FailedTransfer_IsCaughtAsCurlException()
    {
        ScriptedEasyTransfer easy = Scripted();
        easy.Result = CurlCode.CouldntConnect;
        await using CurlClient client = ClientOver(() => easy);

        await ReadmeSamples.CancelAndHandleErrorsAsync(client, CancellationToken.None);

        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task Sample_Progress_GivesTheRequestAProgressSink()
    {
        ProgressEasyTransfer easy = new() { BodyBytes = "hello"u8.ToArray() };
        easy.Calls.Add((10, 5, 0, 0));
        await using CurlClient client = ClientOver(() => easy);

        await ReadmeSamples.ReportProgressAsync(client, CancellationToken.None);

        Assert.HasCount(1, easy.Answers);
    }

    [TestMethod]
    public async Task Sample_Multipart_PostsAMultipartBody()
    {
        ScriptedEasyTransfer easy = Scripted();
        await using CurlClient client = ClientOver(() => easy);

        await ReadmeSamples.UploadMultipartAsync(client, CancellationToken.None);

        Assert.AreEqual("https://example.com/albums", easy.PerformedOptions!.Network.Url);
    }

    [TestMethod]
    public async Task Sample_WebSocket_SendsHelloAndReadsUntilTheClose()
    {
        FakeWebSocketTransfer transfer = new();
        transfer.FakeWebSocket.Queue("hi"u8.ToArray(), CurlWebSocketFlags.Text).Queue([0x03, 0xE8], CurlWebSocketFlags.Close);
        await using CurlClient client = ClientOver(() => transfer);

        await ReadmeSamples.UseWebSocketAsync(client, CancellationToken.None);

        Assert.AreEqual("hello", Encoding.UTF8.GetString(transfer.FakeWebSocket.Sends[0].Data));
    }

    [TestMethod]
    public async Task Sample_Concurrent_SendsEveryUrl()
    {
        List<ScriptedEasyTransfer> handles = [];
        await using CurlClient client = ClientOver(() =>
        {
            ScriptedEasyTransfer easy = Scripted();
            lock (handles)
            {
                handles.Add(easy);
            }

            return easy;
        });

        await ReadmeSamples.RunConcurrentTransfersAsync(client, CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { "https://example.com/a", "https://example.com/b", "https://example.com/c" },
            handles.Select(easy => easy.PerformedOptions!.Network.Url).ToArray());
    }

    [TestMethod]
    public async Task Sample_HttpClient_ReadsTheBodyThroughTheHandler()
    {
        await using CurlClient client = ClientOver(Scripted);

        await ReadmeSamples.UseWithHttpClientAsync(client, CancellationToken.None);
    }

    [TestMethod]
    public void Sample_Usings_AreTheOnesTheSamplesNeed()
    {
        Assert.AreEqual("using System.Net;\nusing Curl.Api;\nusing Curl.Api.Errors;\nusing Curl.Api.Options.Http;", SourceSample("usings"));
    }
}
