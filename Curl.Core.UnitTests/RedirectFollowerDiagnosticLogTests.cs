using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="RedirectFollower" /> writes to Curl's own diagnostic log, component
/// <c>redirect</c> (ADR-0222, BL-921): each hop followed as <c>info</c> with its status, target and
/// method, a redirect not followed as <c>warning</c>, the <c>--max-redirs</c> counters as
/// <c>verbose</c>, never a password from the target; and that every hop carries the wrapped
/// context's log.
/// </summary>
[TestClass]
public sealed class RedirectFollowerDiagnosticLogTests
{
    private const string Url = "http://127.0.0.1:18921/a";

    private const string Target = "http://127.0.0.1:18921/b";

    [TestMethod]
    public async Task FollowAsync_FollowedRedirect_LogsTheHopAsInfoWithoutThePassword()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ScriptedHandler handler = new(Redirect(302, "http://user:s3cret@127.0.0.1:18921/b"), Ok());

        await FollowAsync(handler, log, new HttpRequestOptions { FollowRedirects = true }, new RedirectPolicy());

        CollectionAssert.AreEqual(
            new[] { (DiagnosticLogLevel.Info, DiagnosticLogComponents.Redirect, "following 302 to http://127.0.0.1:18921/b, method kept") },
            log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FollowAsync_PostRedirectedBy302_LogsTheMethodChangedToGet()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ScriptedHandler handler = new(Redirect(302, Target), Ok());
        HttpRequestOptions http = new() { FollowRedirects = true, Body = new BytesBody(new byte[] { 1 }, "application/x-www-form-urlencoded") };

        await FollowAsync(handler, log, http, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { "following 302 to http://127.0.0.1:18921/b, method changed to GET" }, log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task FollowAsync_CustomMethodRedirectedBy303_LogsTheMethodChangedToGet()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        ScriptedHandler handler = new(Redirect(303, Target), Ok());
        HttpRequestOptions http = new() { FollowRedirects = true, CustomMethod = "DELETE" };

        await FollowAsync(handler, log, http, new RedirectPolicy { DropsCustomMethodOnSwitchToGet = true });

        CollectionAssert.AreEqual(new[] { "following 303 to http://127.0.0.1:18921/b, method changed to GET" }, log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task FollowAsync_RefusedRedirect_LogsAWarningWithoutThePasswordAndTheLimitAsVerbose()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Verbose);
        ScriptedHandler handler = new(Redirect(301, "http://user:s3cret@127.0.0.1:18921/b"));

        await FollowAsync(handler, log, new HttpRequestOptions { FollowRedirects = true }, new RedirectPolicy { MaxRedirects = 0 });

        CollectionAssert.AreEqual(
            new[] { "301 to http://127.0.0.1:18921/b not followed: exit 47 (TooManyRedirects), Maximum (0) redirects followed" },
            log.At(DiagnosticLogLevel.Warning));
        CollectionAssert.AreEqual(new[] { "--max-redirs 0, 0 redirects followed" }, log.At(DiagnosticLogLevel.Verbose));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Redirect));
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FollowAsync_AtLogLevelError_RecordsNoInfoOrVerboseLine()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        ScriptedHandler handler = new(Redirect(302, Target), Ok());

        await FollowAsync(handler, log, new HttpRequestOptions { FollowRedirects = true }, new RedirectPolicy());

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task FollowAsync_EveryHop_CarriesTheWrappedContextsDiagnosticLog()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        ScriptedHandler handler = new(Redirect(302, Target), Ok());

        await FollowAsync(handler, log, new HttpRequestOptions { FollowRedirects = true }, new RedirectPolicy());

        Assert.HasCount(2, handler.Logs);
        Assert.IsTrue(handler.Logs.All(carried => ReferenceEquals(carried, log)));
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler, IDiagnosticLog log, HttpRequestOptions http, RedirectPolicy policy) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Url),
                    Output = Stream.Null,
                    Http = http,
                    TimeProvider = TimeProvider.System,
                    DiagnosticLog = log,
                },
                policy)
            .AsTask();

    private static TransferResult Redirect(int status, string location) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    private static TransferResult Ok() => TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 200 } };

    /// <summary>Serves http, answering each call with the next scripted result and keeping each hop's log.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        public List<IDiagnosticLog> Logs { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Logs.Add(context.DiagnosticLog);
            return ValueTask.FromResult(script[Logs.Count - 1]);
        }
    }
}
