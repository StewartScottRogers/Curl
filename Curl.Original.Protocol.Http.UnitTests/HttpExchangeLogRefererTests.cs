using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

[TestClass]
public sealed class HttpExchangeLogRefererTests
{
    [TestMethod]
    [DataRow("Referer", "http://ru:REFSECRET@ref.example/x", "http://REDACTED:REDACTED@ref.example/x")]
    [DataRow("referer", "http://hu:HREFSECRET@x/", "http://REDACTED:REDACTED@x/")]
    [DataRow("Referer", "http://ref.example/x", "http://ref.example/x")]
    [DataRow("Location", "http://u:LOCSECRET@h/p", "http://REDACTED:REDACTED@h/p")]
    [DataRow("Content-Location", "http://u:LOCSECRET@h/p", "http://REDACTED:REDACTED@h/p")]
    public void RequestSent_UrlHeader_LogsItWithUserInfoRedacted(string name, string value, string expected)
    {
        RecordingDiagnosticLog log = new();
        byte[] head = Encoding.ASCII.GetBytes($"GET /plain HTTP/1.1\r\nHost: h\r\n{name}: {value}\r\n\r\n");

        new HttpExchangeLog(log, DiagnosticLogComponents.Http).RequestSent("GET", "/plain", head);

        Assert.IsTrue(log.MessagesAt(DiagnosticLogLevel.Verbose).Contains($"header sent {name}: {expected}"));
        Assert.IsFalse(string.Join("|", log.MessagesAt(DiagnosticLogLevel.Verbose)).Contains("SECRET", StringComparison.Ordinal));
    }
}
