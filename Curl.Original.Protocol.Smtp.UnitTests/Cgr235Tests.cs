namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins CGR-235 (ADR-0976): a failed SMTP response read reports <c>errno: 0</c> on every platform,
/// as curl 8.22.0 does, not the stale <c>errno: 115</c> curl 8.18.0 prints on Linux.
/// </summary>
[TestClass]
public sealed class Cgr235Tests
{
    [TestMethod]
    public void ResponseReadingFailed_ReportsErrnoZero() =>
        StringAssert.EndsWith(string.Concat(SmtpSessionMessages.ResponseReadingFailed, ""), "(errno: 0)");
}
