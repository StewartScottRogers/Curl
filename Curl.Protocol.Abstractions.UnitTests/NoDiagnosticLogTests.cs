using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="NoDiagnosticLog" /> enables no level and writes nothing, and that
/// <see cref="DiagnosticLogLevel" /> and <see cref="DiagnosticLogComponents" /> hold
/// exactly what ADR-0222 decides.
/// </summary>
[TestClass]
public sealed class NoDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(DiagnosticLogLevel.None)]
    [DataRow(DiagnosticLogLevel.Error)]
    [DataRow(DiagnosticLogLevel.Warning)]
    [DataRow(DiagnosticLogLevel.Info)]
    [DataRow(DiagnosticLogLevel.Verbose)]
    public void IsEnabled_AnyLevel_IsFalse(DiagnosticLogLevel level)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("level", level);

        bool enabled = NoDiagnosticLog.Instance.IsEnabled(level);

        diagnostics.Act("enabled", enabled);
        diagnostics.Assert("enabled", false, enabled);
        Assert.IsFalse(NoDiagnosticLog.Instance.IsEnabled(level));
    }

    [TestMethod]
    public void Write_AnyLine_HasNoEffect()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IDiagnosticLog log = NoDiagnosticLog.Instance;
        diagnostics.Arrange("line", "reply HTTP/1.1 500");

        log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Http, "reply HTTP/1.1 500");

        diagnostics.Act("error enabled after write", log.IsEnabled(DiagnosticLogLevel.Error));
        diagnostics.Assert("error enabled after write", false, log.IsEnabled(DiagnosticLogLevel.Error));
        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Error));
        Assert.AreSame(log, NoDiagnosticLog.Instance);
    }

    [TestMethod]
    public void DiagnosticLogLevel_Values_AreCumulativeFromNoneToVerbose()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        int[] values =
        [
            (int)DiagnosticLogLevel.None,
            (int)DiagnosticLogLevel.Error,
            (int)DiagnosticLogLevel.Warning,
            (int)DiagnosticLogLevel.Info,
            (int)DiagnosticLogLevel.Verbose,
        ];
        diagnostics.Arrange("expected values", "0,1,2,3,4");

        diagnostics.Act("values", string.Join(",", values));
        diagnostics.Act("defined level count", Enum.GetValues<DiagnosticLogLevel>().Length);
        diagnostics.Assert("values", "0,1,2,3,4", string.Join(",", values));
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, values);
        Assert.HasCount(5, Enum.GetValues<DiagnosticLogLevel>());
    }

    [TestMethod]
    public void DiagnosticLogComponents_Names_AreExactlyTheAdrList()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] expected =
        [
            "cli", "runner", "dns", "connect", "proxy", "tls", "quic", "http", "http2", "http3",
            "auth", "retry", "redirect", "hsts", "altsvc", "ftp", "tftp", "ssh", "smtp", "imap",
            "pop3", "dict", "gopher", "telnet", "mqtt", "file", "smb", "ldap", "rtsp", "ws",
        ];
        diagnostics.Arrange("expected components", string.Join(",", expected));

        var actual = typeof(DiagnosticLogComponents)
            .GetFields()
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        diagnostics.Act("actual components", string.Join(",", actual));
        diagnostics.Assert("component count", expected.Length, actual.Length);
        CollectionAssert.AreEquivalent(expected, actual);
    }
}
