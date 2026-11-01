using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-047 (BL-3576, ADR-0821): measured on curl 8.22.0 (Windows) on 2026-10-26, an unreadable
/// <c>Last-Modified</c> is the epoch, so <c>-R</c> gives the file the epoch and <c>-z</c> takes the
/// condition as met, and <c>Wed, 31 Dec 1969 23:59:59 GMT</c> is -1, so <c>-R</c> leaves the file's
/// time alone and <c>-z</c> compares it.
/// </summary>
[TestClass]
public sealed class Cgr047Tests
{
    private const string OneSecondBeforeTheEpoch = "Wed, 31 Dec 1969 23:59:59 GMT";

    private static readonly DateTimeOffset ConditionTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("garbage")]
    [DataRow("")]
    [DataRow("x")]
    [DataRow("1445412480")]
    [DataRow("2015-10-21T07:28:00Z")]
    [DataRow("Wed, 32 Oct 2015 07:28:00 GMT")]
    [DataRow("Wed, 21 Oct 2015 25:28:00 GMT")]
    public void FindFileTime_UnreadableLastModified_IsTheEpoch(string value)
    {
        Diagnostics.Arrange("Last-Modified", value);

        long? found = HttpLastModified.FindFileTime(Head("Last-Modified: " + value));

        Diagnostics.Act("file time", found?.ToString() ?? "(none)");
        Diagnostics.Assert("file time", "0", found?.ToString() ?? "(none)");
        Assert.AreEqual(0L, found);
    }

    [TestMethod]
    public void FindFileTime_OneSecondBeforeTheEpoch_IsNoTime()
    {
        Diagnostics.Arrange("Last-Modified", OneSecondBeforeTheEpoch);

        long? found = HttpLastModified.FindFileTime(Head("Last-Modified: " + OneSecondBeforeTheEpoch));

        Diagnostics.Act("file time", found?.ToString() ?? "(none)");
        Diagnostics.Assert("file time", "(none)", found?.ToString() ?? "(none)");
        Assert.IsNull(found);
    }

    [TestMethod]
    public void FindFileTime_NoLastModified_IsNoTime()
    {
        Diagnostics.Arrange("header", "X-A: 1");

        long? found = HttpLastModified.FindFileTime(Head("X-A: 1"));

        Diagnostics.Act("file time", found?.ToString() ?? "(none)");
        Diagnostics.Assert("file time", "(none)", found?.ToString() ?? "(none)");
        Assert.IsNull(found);
    }

    [TestMethod]
    public void FindFileTime_ReadableLastModified_GivesItsTime()
    {
        Diagnostics.Arrange("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT");

        long? found = HttpLastModified.FindFileTime(Head("Last-Modified: Wed, 21 Oct 2015 07:28:00 GMT"));

        Diagnostics.Act("file time", found);
        Diagnostics.Assert("file time", 1_445_412_480L, found);
        Assert.AreEqual(1_445_412_480L, found);
    }

    [TestMethod]
    public void Find_OneSecondBeforeTheEpoch_IsMinusOne()
    {
        Diagnostics.Arrange("Last-Modified", OneSecondBeforeTheEpoch);

        long? found = HttpLastModified.Find(Head("Last-Modified: " + OneSecondBeforeTheEpoch));

        Diagnostics.Act("document time", found);
        Diagnostics.Assert("document time", -1L, found);
        Assert.AreEqual(-1L, found);
    }

    /// <summary>curl 8.22.0 delivers the body for <c>-z "Jan 1 2000"</c> when the document time is the epoch.</summary>
    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince)]
    [DataRow(TimeConditionKind.IfUnmodifiedSince)]
    public void IsMet_DocumentTimeAtTheEpoch_IsMet(TimeConditionKind kind)
    {
        Diagnostics.Arrange("condition", kind);

        bool met = HttpDownloadConditions.IsMet(new TimeCondition(ConditionTime, kind), 0);

        Diagnostics.Act("met", met);
        Diagnostics.Assert("met", true, met);
        Assert.IsTrue(met);
    }

    /// <summary>curl 8.22.0 answers <c>-z "Jan 1 2000"</c> with 304 for a document one second before the epoch, and delivers for <c>-z "-Jan 1 2000"</c>.</summary>
    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, false)]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, true)]
    public void IsMet_DocumentTimeOneSecondBeforeTheEpoch_IsCompared(TimeConditionKind kind, bool expected)
    {
        Diagnostics.Arrange("condition", kind);

        bool met = HttpDownloadConditions.IsMet(new TimeCondition(ConditionTime, kind), -1);

        Diagnostics.Act("met", met);
        Diagnostics.Assert("met", expected, met);
        Assert.AreEqual(expected, met);
    }

    private static HttpResponseHead Head(string header) =>
        new(
            HttpStatusLine.Parse("HTTP/1.1 200 X"),
            [new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim())],
            default,
            default);
}
