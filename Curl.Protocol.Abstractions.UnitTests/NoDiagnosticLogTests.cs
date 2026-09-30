namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="NoDiagnosticLog" /> enables no level and writes nothing, and that
/// <see cref="DiagnosticLogLevel" /> and <see cref="DiagnosticLogComponents" /> hold
/// exactly what ADR-0222 decides.
/// </summary>
[TestClass]
public sealed class NoDiagnosticLogTests
{
    [TestMethod]
    [DataRow(DiagnosticLogLevel.None)]
    [DataRow(DiagnosticLogLevel.Error)]
    [DataRow(DiagnosticLogLevel.Warning)]
    [DataRow(DiagnosticLogLevel.Info)]
    [DataRow(DiagnosticLogLevel.Verbose)]
    public void IsEnabled_AnyLevel_IsFalse(DiagnosticLogLevel level)
    {
        Assert.IsFalse(NoDiagnosticLog.Instance.IsEnabled(level));
    }

    [TestMethod]
    public void Write_AnyLine_HasNoEffect()
    {
        IDiagnosticLog log = NoDiagnosticLog.Instance;

        log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Http, "reply HTTP/1.1 500");

        Assert.IsFalse(log.IsEnabled(DiagnosticLogLevel.Error));
        Assert.AreSame(log, NoDiagnosticLog.Instance);
    }

    [TestMethod]
    public void DiagnosticLogLevel_Values_AreCumulativeFromNoneToVerbose()
    {
        int[] values =
        [
            (int)DiagnosticLogLevel.None,
            (int)DiagnosticLogLevel.Error,
            (int)DiagnosticLogLevel.Warning,
            (int)DiagnosticLogLevel.Info,
            (int)DiagnosticLogLevel.Verbose,
        ];

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, values);
        Assert.HasCount(5, Enum.GetValues<DiagnosticLogLevel>());
    }

    [TestMethod]
    public void DiagnosticLogComponents_Names_AreExactlyTheAdrList()
    {
        string[] expected =
        [
            "cli", "runner", "dns", "connect", "proxy", "tls", "quic", "http", "http2", "http3",
            "auth", "retry", "redirect", "hsts", "altsvc", "ftp", "tftp", "ssh", "smtp", "imap",
            "pop3", "dict", "gopher", "telnet", "mqtt", "file", "smb", "ldap", "rtsp", "ws",
        ];

        var actual = typeof(DiagnosticLogComponents)
            .GetFields()
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        CollectionAssert.AreEquivalent(expected, actual);
    }
}
