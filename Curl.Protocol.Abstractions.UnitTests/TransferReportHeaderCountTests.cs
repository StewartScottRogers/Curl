using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="TransferReport.HeaderCount" />, what <c>%{num_headers}</c> prints: the trailers alone,
/// else the report's own header lines, else the count a followed response left (ADR-1477).
/// </summary>
[TestClass]
public sealed class TransferReportHeaderCountTests
{
    private static readonly KeyValuePair<string, string> Header = new("Server", "x");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HeaderCount_NothingReceived_IsZero() => AssertCount(0, new TransferReport());

    [TestMethod]
    public void HeaderCount_OwnHeaderLines_AddsEverySource() => AssertCount(8, new TransferReport
    {
        ResponseHeaders = [Header],
        PseudoHeaders = [Header, Header],
        ConnectReplyHeaderCount = 2,
        InformationalHeaderCount = 3,
        FollowedHeaderCount = 40,
    });

    [TestMethod]
    public void HeaderCount_Trailers_IsTheirCountAlone() => AssertCount(2, new TransferReport
    {
        ResponseHeaders = [Header, Header, Header],
        TrailerCount = 2,
        FollowedHeaderCount = 40,
    });

    [TestMethod]
    public void HeaderCount_NoOwnHeaderLines_IsTheFollowedCount() => AssertCount(4, new TransferReport { FollowedHeaderCount = 4 });

    private void AssertCount(int expected, TransferReport report)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("followed header count", report.FollowedHeaderCount);

        int actual = report.HeaderCount;

        diagnostics.Act("header count", actual);
        diagnostics.Assert("header count", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
