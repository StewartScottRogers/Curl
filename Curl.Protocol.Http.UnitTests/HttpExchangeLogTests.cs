using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpExchangeLogTests
{
    [TestMethod]
    [DataRow(null, "http", DisplayName = "HTTP/1.x")]
    [DataRow("HTTP/2", "http2", DisplayName = "HTTP/2")]
    [DataRow("HTTP/3", "http3", DisplayName = "HTTP/3")]
    public void ComponentOf_VersionName_GivesItsComponent(string? versionName, string component) =>
        Assert.AreEqual(component, HttpExchangeLog.ComponentOf(versionName));

    [TestMethod]
    public void For_NoSession_UsesComponentHttp() =>
        Assert.AreEqual(DiagnosticLogComponents.Http, HttpExchangeLog.For(NoDiagnosticLog.Instance, null).Component);

    [TestMethod]
    public void Silent_LogsNoInfo() =>
        Assert.IsFalse(HttpExchangeLog.Silent.LogsInfo);

    [TestMethod]
    [DataRow(true, "using HTTP/2 on a new connection")]
    [DataRow(false, "using HTTP/2 on a reused connection")]
    public void VersionChosen_AtVerbose_LogsTheVersionAndConnection(bool newConnection, string expected)
    {
        RecordingDiagnosticLog log = new();

        new HttpExchangeLog(log, DiagnosticLogComponents.Http2).VersionChosen("HTTP/2", newConnection);

        Assert.AreEqual((DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Http2, expected), log.Lines.Single());
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http3, false, "HTTP/1.x", "--http3 fell back to HTTP/1.x over TCP")]
    [DataRow(HttpVersionPreference.Http3, true, "HTTP/2", "--http3 fell back to HTTP/2 over TCP")]
    [DataRow(HttpVersionPreference.Http2, true, "HTTP/1.x", "--http2 refused by ALPN; using HTTP/1.x")]
    public void DowngradeOf_VersionNotSpoken_LogsAWarning(HttpVersionPreference asked, bool secure, string versionName, string expected)
    {
        RecordingDiagnosticLog log = new();

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).DowngradeOf(asked, secure, versionName);

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

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).DowngradeOf(asked, secure, versionName);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public void RequestSent_AtVerbose_LogsThePathAndEachHeaderHidingSecretValues()
    {
        RecordingDiagnosticLog log = new();
        byte[] head = Encoding.Latin1.GetBytes("GET /a HTTP/1.1\r\nHost: h\r\nproxy-authorization: Basic eDp5\r\nOdd line\r\n\r\n");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).RequestSent("GET", "/a", head);

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

        new HttpExchangeLog(log, DiagnosticLogComponents.Http2).ReplyRead(head);

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

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(isChunked, contentLength), ["gzip"]);

        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Http, expected), log.Lines.Single());
    }

    [TestMethod]
    public void BodyFramed_AtVerboseWithCodings_LogsTheDecoderChain()
    {
        RecordingDiagnosticLog log = new();

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(false, 3), ["gzip", "br"]);

        CollectionAssert.AreEqual(new[] { "decoders gzip, br" }, log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void BodyFramed_AtVerboseWithoutCodings_LogsNoDecoderChain()
    {
        RecordingDiagnosticLog log = new();

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).BodyFramed(new HttpResponseBodyFraming(false, 3), []);

        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void Exchanged_AtInfo_LogsStatusBytesAndMilliseconds()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        new HttpExchangeLog(log, DiagnosticLogComponents.Http3).Exchanged(200, 12, TimeSpan.FromMilliseconds(34.9));

        Assert.AreEqual((DiagnosticLogLevel.Info, DiagnosticLogComponents.Http3, "exchange done: status 200, 12 body bytes in 34 ms"), log.Lines.Single());
    }

    [TestMethod]
    public void Failed_AtError_LogsTheExitCodeMessageAndExceptionType()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).Failed(CurlExitCode.OperationTimedOut, new OperationCanceledException("gone"));

        Assert.AreEqual(
            (DiagnosticLogLevel.Error, DiagnosticLogComponents.Http, "exchange failed with OperationTimedOut (exit 28): gone [OperationCanceledException]"),
            log.Lines.Single());
    }

    [TestMethod]
    public void RetryingOnFreshConnection_AtWarning_LogsTheRetry()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).RetryingOnFreshConnection(2);

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

        exchangeLog.VersionChosen("HTTP/1.x", newConnection: true);
        exchangeLog.DowngradeOf(HttpVersionPreference.Http3, false, "HTTP/1.x");
        exchangeLog.RequestSent("GET", "/", "GET / HTTP/1.1\r\nA: b\r\n\r\n"u8.ToArray());
        exchangeLog.ReplyRead(head);
        exchangeLog.BodyFramed(new HttpResponseBodyFraming(true, null), ["gzip"]);
        exchangeLog.Exchanged(200, 0, TimeSpan.Zero);
        exchangeLog.Failed(CurlExitCode.RecvError, new IOException("x"));
        exchangeLog.RetryingOnFreshConnection(1);

        Assert.IsFalse(exchangeLog.LogsInfo);
        Assert.IsEmpty(log.Lines);
    }
}
