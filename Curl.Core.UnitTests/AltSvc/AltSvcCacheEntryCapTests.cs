using System.Text;

namespace Curl.Core.AltSvc;

/// <summary>
/// Pins <see cref="AltSvcCache.MaxEntries" />, libcurl 8.22.0's <c>MAX_ALTSVC_ENTRIES</c> of 5000, which
/// upstream's unit1669 found missing (ADR-0482): a full cache drops its oldest entry before it adds one, from
/// the file and from a header alike, as <c>altsvc_append</c> does.
/// </summary>
[TestClass]
public sealed class AltSvcCacheEntryCapTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1548369261);

    [TestMethod]
    public void ReadFile_MoreLinesThanTheCap_KeepsTheLastFiveThousand()
    {
        AltSvcCache cache = new(new FixedClock(Now));

        cache.ReadFile(FileOf(AltSvcCache.MaxEntries + 10));

        Assert.HasCount(5000, cache.Entries);
        Assert.AreEqual(11, cache.Entries[0].SourcePort);
        Assert.AreEqual(5010, cache.Entries[^1].SourcePort);
    }

    [TestMethod]
    public void ApplyHeader_FullCache_DropsTheOldestEntryAndAddsTheNewOneLast()
    {
        AltSvcCache cache = new(new FixedClock(Now));
        cache.ReadFile(FileOf(AltSvcCache.MaxEntries));

        cache.ApplyHeader("h2=\"new.example:443\"", AltSvcAlpn.H1, "origin.example", 443);

        Assert.HasCount(5000, cache.Entries);
        Assert.AreEqual(2, cache.Entries[0].SourcePort);
        Assert.AreEqual("new.example", cache.Entries[^1].DestinationHost);
    }

    private static string FileOf(int lineCount)
    {
        StringBuilder text = new();
        for (int port = 0; port < lineCount; port++)
        {
            text.Append("h2 example.com ").Append(port + 1).Append(" h3 shiny.example.com 8443 \"20191231 00:00:00\" 0 0\n");
        }

        return text.ToString();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
