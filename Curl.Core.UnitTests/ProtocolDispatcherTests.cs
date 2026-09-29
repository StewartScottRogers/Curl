using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="ProtocolDispatcher" /> picks a handler by scheme and how it
/// reports a scheme no handler serves, measured against curl 8.21.0.
/// </summary>
[TestClass]
public sealed class ProtocolDispatcherTests
{
    [TestMethod]
    public async Task DispatchAsync_FileUrl_ReachesOnlyFileHandlerWithSameContextAndReturnsItsResult()
    {
        TransferResult fileResult = TransferResult.Success(42);
        RecordingHandler file = new(fileResult, "file");
        RecordingHandler dict = new(TransferResult.Success(7), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);
        TransferContext context = CreateContext("file:///x");

        TransferResult result = await dispatcher.DispatchAsync(context);

        Assert.AreSame(fileResult, result);
        Assert.HasCount(1, file.ReceivedContexts);
        Assert.AreSame(context, file.ReceivedContexts[0]);
        Assert.IsEmpty(dict.ReceivedContexts);
    }

    [TestMethod]
    public async Task DispatchAsync_UppercaseSchemeOfRegisteredHandler_ReachesThatHandler()
    {
        TransferResult dictResult = TransferResult.Success(3);
        RecordingHandler dict = new(dictResult, "dict");
        ProtocolDispatcher dispatcher = new([dict]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("DICT://host/"));

        Assert.AreSame(dictResult, result);
        Assert.HasCount(1, dict.ReceivedContexts);
    }

    [TestMethod]
    public async Task DispatchAsync_UnregisteredUppercaseScheme_ReturnsExit1WithLowercasedSchemeAndCallsNoHandler()
    {
        RecordingHandler file = new(TransferResult.Success(0), "file");
        RecordingHandler dict = new(TransferResult.Success(0), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("XYZ://foo"));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"xyz\" not supported", result.ErrorMessage);
        Assert.IsEmpty(file.ReceivedContexts);
        Assert.IsEmpty(dict.ReceivedContexts);
    }

    [TestMethod]
    [DataRow("dict://127.0.0.1:48523/", "dict")]
    [DataRow("DICT://127.0.0.1:48523/", "dict")]
    [DataRow("file:///dir/x", "file")]
    public async Task DispatchAsync_SchemeProtoExcludes_ReturnsExit1ProtocolDisabledAndCallsNoHandler(string url, string scheme)
    {
        // Measured against curl 8.21.0 on 2026-09-28 (BL-523 Notes): curl -sS --proto =https
        // http://127.0.0.1:48523/ -> exit 1, "curl: (1) Protocol "http" is disabled", no connection;
        // --proto =http file:///dir/x -> "Protocol "file" is disabled".
        RecordingHandler file = new(TransferResult.Success(0), "file");
        RecordingHandler dict = new(TransferResult.Success(0), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext(url), new HashSet<string>(["https"]));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" is disabled", result.ErrorMessage);
        Assert.IsEmpty(file.ReceivedContexts);
        Assert.IsEmpty(dict.ReceivedContexts);
    }

    [TestMethod]
    public async Task DispatchAsync_SchemeProtoAllows_ReachesItsHandler()
    {
        TransferResult dictResult = TransferResult.Success(3);
        RecordingHandler dict = new(dictResult, "dict");
        ProtocolDispatcher dispatcher = new([dict]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("DICT://host/"), new HashSet<string>(["dict"]));

        Assert.AreSame(dictResult, result);
    }

    [TestMethod]
    public async Task DispatchAsync_UnservedSchemeProtoExcludes_ReturnsNotSupportedRatherThanDisabled()
    {
        // curl -sS --proto =http bogus://127.0.0.1:48523/ -> exit 1, "Protocol "bogus" not supported" (BL-523 Notes).
        ProtocolDispatcher dispatcher = new([new RecordingHandler(TransferResult.Success(0), "http")]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("bogus://127.0.0.1:48523/"), new HashSet<string>(["http"]));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"bogus\" not supported", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("file:///x")]
    [DataRow("http://example.com/")]
    [DataRow("dict://host/")]
    public async Task DispatchAsync_EmptyHandlerSet_ReturnsExit1(string url)
    {
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(CreateContext(url));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
    }

    [TestMethod]
    public void Constructor_TwoHandlersClaimDict_ThrowsArgumentExceptionNamingDict()
    {
        RecordingHandler first = new(TransferResult.Success(0), "dict");
        RecordingHandler second = new(TransferResult.Success(0), "file", "dict");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ProtocolDispatcher([first, second]));

        Assert.Contains("dict", exception.Message);
    }

    private static TransferContext CreateContext(string url) =>
        new() { Url = CurlUrl.Parse(url), Output = Stream.Null };

    private sealed class RecordingHandler(TransferResult result, params string[] schemes)
        : IProtocolHandler
    {
        public List<ITransferContext> ReceivedContexts { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => schemes;

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            ReceivedContexts.Add(context);
            return ValueTask.FromResult(result);
        }
    }
}
