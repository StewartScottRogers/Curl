using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="ProtocolDispatcher" /> picks a handler by scheme and how it
/// reports a scheme no handler serves, measured against curl 8.21.0.
/// </summary>
[TestClass]
public sealed class ProtocolDispatcherTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DispatchAsync_FileUrl_ReachesOnlyFileHandlerWithSameContextAndReturnsItsResult()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult fileResult = TransferResult.Success(42);
        RecordingHandler file = new(fileResult, "file");
        RecordingHandler dict = new(TransferResult.Success(7), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);
        TransferContext context = CreateContext("file:///x");
        diagnostics.Arrange("url", "file:///x");
        diagnostics.Arrange("handlers", "file | dict");

        TransferResult result;
        using (diagnostics.Phase("dispatch"))
        {
            result = await dispatcher.DispatchAsync(context);
        }

        diagnostics.Act("same result as file handler", ReferenceEquals(fileResult, result));
        diagnostics.Act("file handler contexts", file.ReceivedContexts.Count);
        diagnostics.Act("dict handler contexts", dict.ReceivedContexts.Count);
        diagnostics.Assert("file handler contexts", 1, file.ReceivedContexts.Count);
        diagnostics.Assert("dict handler contexts", 0, dict.ReceivedContexts.Count);
        Assert.AreSame(fileResult, result);
        Assert.HasCount(1, file.ReceivedContexts);
        Assert.AreSame(context, file.ReceivedContexts[0]);
        Assert.IsEmpty(dict.ReceivedContexts);
    }

    [TestMethod]
    [DataRow("dict", true)]
    [DataRow("DICT", true)]
    [DataRow("qttp", false)]
    public void Serves_Scheme_SaysWhetherAHandlerIsRegisteredForItInAnyCase(string scheme, bool served)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ProtocolDispatcher dispatcher = new([new RecordingHandler(TransferResult.Success(0), "dict")]);
        diagnostics.Arrange("scheme", scheme);
        diagnostics.Arrange("registered", "dict");

        bool actual = dispatcher.Serves(scheme);

        diagnostics.Act("serves", actual);
        diagnostics.Assert("serves", served, actual);
        Assert.AreEqual(served, actual);
    }

    [TestMethod]
    public async Task DispatchAsync_UppercaseSchemeOfRegisteredHandler_ReachesThatHandler()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult dictResult = TransferResult.Success(3);
        RecordingHandler dict = new(dictResult, "dict");
        ProtocolDispatcher dispatcher = new([dict]);
        diagnostics.Arrange("url", "DICT://host/");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("DICT://host/"));

        diagnostics.Act("same result as dict handler", ReferenceEquals(dictResult, result));
        diagnostics.Act("dict handler contexts", dict.ReceivedContexts.Count);
        diagnostics.Assert("dict handler contexts", 1, dict.ReceivedContexts.Count);
        Assert.AreSame(dictResult, result);
        Assert.HasCount(1, dict.ReceivedContexts);
    }

    [TestMethod]
    public async Task DispatchAsync_UnregisteredUppercaseScheme_ReturnsExit1WithLowercasedSchemeAndCallsNoHandler()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingHandler file = new(TransferResult.Success(0), "file");
        RecordingHandler dict = new(TransferResult.Success(0), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);
        diagnostics.Arrange("url", "XYZ://foo");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("XYZ://foo"));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        diagnostics.Assert("error message", "Protocol \"xyz\" not supported", result.ErrorMessage);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingHandler file = new(TransferResult.Success(0), "file");
        RecordingHandler dict = new(TransferResult.Success(0), "dict");
        ProtocolDispatcher dispatcher = new([file, dict]);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("allowed protocols", "https");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext(url), new HashSet<string>(["https"]));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        diagnostics.Assert("error message", $"Protocol \"{scheme}\" is disabled", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" is disabled", result.ErrorMessage);
        Assert.IsEmpty(file.ReceivedContexts);
        Assert.IsEmpty(dict.ReceivedContexts);
    }

    [TestMethod]
    public async Task DispatchAsync_SchemeProtoAllows_ReachesItsHandler()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult dictResult = TransferResult.Success(3);
        RecordingHandler dict = new(dictResult, "dict");
        ProtocolDispatcher dispatcher = new([dict]);
        diagnostics.Arrange("url", "DICT://host/");
        diagnostics.Arrange("allowed protocols", "dict");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("DICT://host/"), new HashSet<string>(["dict"]));

        diagnostics.Act("same result as dict handler", ReferenceEquals(dictResult, result));
        diagnostics.Assert("same result as dict handler", true, ReferenceEquals(dictResult, result));
        Assert.AreSame(dictResult, result);
    }

    [TestMethod]
    public async Task DispatchAsync_UnservedSchemeProtoExcludes_ReturnsNotSupportedRatherThanDisabled()
    {
        // curl -sS --proto =http bogus://127.0.0.1:48523/ -> exit 1, "Protocol "bogus" not supported" (BL-523 Notes).
        var diagnostics = TestDiagnostics.For(TestContext);
        ProtocolDispatcher dispatcher = new([new RecordingHandler(TransferResult.Success(0), "http")]);
        diagnostics.Arrange("url", "bogus://127.0.0.1:48523/");
        diagnostics.Arrange("allowed protocols", "http");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext("bogus://127.0.0.1:48523/"), new HashSet<string>(["http"]));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        diagnostics.Assert("error message", "Protocol \"bogus\" not supported", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"bogus\" not supported", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("bogus://127.0.0.1:48806/", "Protocol \"bogus\" not supported")]
    [DataRow("dict://127.0.0.1:48805/", "Protocol \"dict\" is disabled")]
    public async Task DispatchAsync_RefusedScheme_ReportsTheRefusalAsAnInfoLine(string url, string message)
    {
        // Measured against curl 8.21.0 on 2026-10-01 (BL-805 Notes): curl -v --proto -http
        // http://127.0.0.1:48805/ writes "* Protocol "http" is disabled" before its curl: (1) line, and
        // curl -v --proto =http bogus://... writes "* Protocol "bogus" not supported".
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = new();
        ProtocolDispatcher dispatcher = new([new RecordingHandler(TransferResult.Success(0), "http", "dict")]);
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = Stream.Null, Events = events };
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("allowed protocols", "http");

        TransferResult result = await dispatcher.DispatchAsync(context, new HashSet<string>(["http"]));

        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Act("info lines", string.Join(" | ", events.Infos));
        diagnostics.Assert("error message", message, result.ErrorMessage);
        diagnostics.Assert("info lines", message, string.Join(" | ", events.Infos));
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { message }, events.Infos);
    }

    [TestMethod]
    [DataRow("file:///x")]
    [DataRow("http://example.com/")]
    [DataRow("dict://host/")]
    public async Task DispatchAsync_EmptyHandlerSet_ReturnsExit1(string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ProtocolDispatcher dispatcher = new([]);
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("handlers", "none");

        TransferResult result = await dispatcher.DispatchAsync(CreateContext(url));

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
    }

    [TestMethod]
    public void Constructor_TwoHandlersClaimDict_ThrowsArgumentExceptionNamingDict()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingHandler first = new(TransferResult.Success(0), "dict");
        RecordingHandler second = new(TransferResult.Success(0), "file", "dict");
        diagnostics.Arrange("first handler schemes", "dict");
        diagnostics.Arrange("second handler schemes", "file | dict");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => new ProtocolDispatcher([first, second]));

        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("message names dict", true, exception.Message.Contains("dict", StringComparison.Ordinal));
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
