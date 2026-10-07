using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpExchangeLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(null, "http", DisplayName = "HTTP/1.x")]
    [DataRow("HTTP/2", "http2", DisplayName = "HTTP/2")]
    [DataRow("HTTP/3", "http3", DisplayName = "HTTP/3")]
    public void ComponentOf_VersionName_GivesItsComponent(string? versionName, string component)
    {
        Diagnostics.Arrange("version name", versionName ?? "(none)");

        string actual = HttpExchangeLog.ComponentOf(versionName);

        Diagnostics.Act("component", actual);
        Diagnostics.Assert("component", component, actual);
        Assert.AreEqual(component, actual);
    }

    [TestMethod]
    public void For_NoSession_UsesComponentHttp()
    {
        Diagnostics.Arrange("session", "none");

        string component = HttpExchangeLog.For(NoDiagnosticLog.Instance, null).Component;

        Diagnostics.Act("component", component);
        Diagnostics.Assert("component", DiagnosticLogComponents.Http, component);
        Assert.AreEqual(DiagnosticLogComponents.Http, component);
    }

    [TestMethod]
    public void Silent_LogsNoInfo()
    {
        Diagnostics.Arrange("log", "HttpExchangeLog.Silent");

        bool logsInfo = HttpExchangeLog.Silent.LogsInfo;

        Diagnostics.Act("logs info", logsInfo);
        Diagnostics.Assert("logs info", false, logsInfo);
        Assert.IsFalse(logsInfo);
    }

    [TestMethod]
    [DataRow(true, "using HTTP/2 on a new connection")]
    [DataRow(false, "using HTTP/2 on a reused connection")]
    public void VersionChosen_AtVerbose_LogsTheVersionAndConnection(bool newConnection, string expected)
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("version, new connection", $"HTTP/2, {newConnection}");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http2).VersionChosen("HTTP/2", newConnection);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", $"Verbose http2 {expected}", Logged(log));
        Assert.AreEqual((DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Http2, expected), log.Lines.Single());
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http3, false, "HTTP/1.x", "--http3 fell back to HTTP/1.x over TCP")]
    [DataRow(HttpVersionPreference.Http3, true, "HTTP/2", "--http3 fell back to HTTP/2 over TCP")]
    [DataRow(HttpVersionPreference.Http2, true, "HTTP/1.x", "--http2 refused by ALPN; using HTTP/1.x")]
    public void DowngradeOf_VersionNotSpoken_LogsAWarning(HttpVersionPreference asked, bool secure, string versionName, string expected)
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("asked, secure, spoken", $"{asked}, {secure}, {versionName}");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).DowngradeOf(asked, secure, versionName);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", $"Warning http {expected}", Logged(log));
        Assert.AreEqual((DiagnosticLogLevel.Warning, DiagnosticLogComponents.Http, expected), log.Lines.Single());
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http3, true, "HTTP/3", DisplayName = "HTTP/3 spoken")]
    [DataRow(HttpVersionPreference.Http2, false, "HTTP/1.x", DisplayName = "h2c over cleartext")]
    [DataRow(HttpVersionPreference.Http2, true, "HTTP/2", DisplayName = "HTTP/2 spoken")]
    [DataRow(HttpVersionPreference.Http11, true, "HTTP/1.x", DisplayName = "HTTP/1.1 asked")]
    public void DowngradeOf_VersionSpoken_LogsNothing(HttpVersionPreference asked, bool secure, string versionName)
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("asked, secure, spoken", $"{asked}, {secure}, {versionName}");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).DowngradeOf(asked, secure, versionName);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public void RequestSent_AtVerbose_LogsThePathAndEachHeaderHidingSecretValues()
    {
        RecordingDiagnosticLog log = new();
        byte[] head = Encoding.Latin1.GetBytes("GET /a HTTP/1.1\r\nHost: h\r\nproxy-authorization: Basic eDp5\r\nOdd line\r\n\r\n");
        Diagnostics.Arrange("method, path", "GET, /a");
        Diagnostics.Bytes("request head", head);

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).RequestSent("GET", "/a", head);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("info", "GET /a sent", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        Diagnostics.Assert(
            "verbose",
            "header sent Host: h | header sent proxy-authorization: (value not logged) | header sent Odd line",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "GET /a sent" }, log.MessagesAt(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "header sent Host: h", "header sent proxy-authorization: (value not logged)", "header sent Odd line" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void ReplyRead_AtVerbose_LogsTheStatusAndEachHeader()
    {
        RecordingDiagnosticLog log = new();
        HttpResponseHead head = new(new HttpStatusLine(new Version(2, 0), 204, string.Empty), [new("Set-Cookie", "a=b"), new("ETag", "\"x\"")], ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
        Diagnostics.Arrange("reply", "HTTP/2 204, Set-Cookie: a=b, ETag: \"x\"");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http2).ReplyRead(head);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("info", "reply 204", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        Diagnostics.Assert(
            "verbose",
            "header received Set-Cookie: (value not logged) | header received ETag: \"x\"",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "reply 204" }, log.MessagesAt(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "header received Set-Cookie: (value not logged)", "header received ETag: \"x\"" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    [DataRow(true, null, "body framed chunked")]
    [DataRow(false, 7L, "body framed by Content-Length 7")]
    [DataRow(false, null, "body framed until the connection closes")]
    public void BodyFramed_AtInfo_LogsTheFraming(bool isChunked, long? contentLength, string expected)
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("chunked, Content-Length", $"{isChunked}, {contentLength?.ToString() ?? "(none)"}");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(isChunked, contentLength), ["gzip"]);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", $"Info http {expected}", Logged(log));
        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Http, expected), log.Lines.Single());
    }

    [TestMethod]
    public void BodyFramed_AtVerboseWithCodings_LogsTheDecoderChain()
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("codings", "gzip, br");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(false, 3), ["gzip", "br"]);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("verbose", "decoders gzip, br", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(new[] { "decoders gzip, br" }, log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void BodyFramed_AtVerboseWithoutCodings_LogsNoDecoderChain()
    {
        RecordingDiagnosticLog log = new();
        Diagnostics.Arrange("codings", "none");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(false, 3), []);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("verbose line count", 0, log.MessagesAt(DiagnosticLogLevel.Verbose).Length);
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void Exchanged_AtInfo_LogsStatusBytesAndMilliseconds()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("status, bytes, elapsed", "200, 12, 34.9 ms");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http3).Exchanged(200, 12, TimeSpan.FromMilliseconds(34.9));

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", "Info http3 exchange done: status 200, 12 body bytes in 34 ms", Logged(log));
        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Http3, "exchange done: status 200, 12 body bytes in 34 ms"), log.Lines.Single());
    }

    [TestMethod]
    public void Failed_AtError_LogsTheExitCodeMessageAndExceptionType()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("exit code, exception", "OperationTimedOut, OperationCanceledException(\"gone\")");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).Failed(CurlExitCode.OperationTimedOut, new OperationCanceledException("gone"));

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", "Error http exchange failed with OperationTimedOut (exit 28): gone [OperationCanceledException]", Logged(log));
        Assert.AreEqual(
            (DiagnosticLogLevel.Error, DiagnosticLogComponents.Http, "exchange failed with OperationTimedOut (exit 28): gone [OperationCanceledException]"),
            log.Lines.Single());
    }

    [TestMethod]
    public void RetryingOnFreshConnection_AtWarning_LogsTheRetry()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        Diagnostics.Arrange("retry", 2);

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).RetryingOnFreshConnection(2);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", "Warning http reused connection died before the reply; sending the request again on a new connection (retry 2)", Logged(log));
        Assert.AreEqual(
            (DiagnosticLogLevel.Warning, DiagnosticLogComponents.Http, "reused connection died before the reply; sending the request again on a new connection (retry 2)"),
            log.Lines.Single());
    }

    [TestMethod]
    public void EveryStep_LogDisabled_WritesNothing()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.None);
        HttpExchangeLog exchangeLog = new(log, DiagnosticLogComponents.Http);
        HttpResponseHead head = new(new HttpStatusLine(new Version(1, 1), 200, "OK"), [new("A", "b")], ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.None);

        exchangeLog.VersionChosen("HTTP/1.x", newConnection: true);
        exchangeLog.DowngradeOf(HttpVersionPreference.Http3, false, "HTTP/1.x");
        exchangeLog.RequestSent("GET", "/", "GET / HTTP/1.1\r\nA: b\r\n\r\n"u8.ToArray());
        exchangeLog.ReplyRead(head);
        exchangeLog.BodyFramed(new HttpResponseBodyFraming(true, null), ["gzip"]);
        exchangeLog.Exchanged(200, 0, TimeSpan.Zero);
        exchangeLog.Failed(CurlExitCode.RecvError, new IOException("x"));
        exchangeLog.RetryingOnFreshConnection(1);

        Diagnostics.Act("logs info, logged", $"{exchangeLog.LogsInfo}, {Logged(log)}");
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsFalse(exchangeLog.LogsInfo);
        Assert.IsEmpty(log.Lines);
    }

    private static string Logged(RecordingDiagnosticLog log) =>
        log.Lines.Count == 0 ? "(nothing)" : string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Component} {line.Message}"));
}
