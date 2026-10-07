using Curl.Kerberos;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>Pins <see cref="KerberosDnsSrvLookup" />: the hand-built DNS client's SRV records as <c>Curl.Kerberos</c> reads them, and a failed lookup as none.</summary>
[TestClass]
public sealed class KerberosDnsSrvLookupTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task LookUpAsync_RecordsFound_ReturnsThemInAnswerOrder()
    {
        List<string> names = [];
        KerberosDnsSrvLookup lookup = new((name, _) =>
        {
            names.Add(name);
            return ValueTask.FromResult(new DnsServiceLookup([new DnsServiceRecord(10, 5, 88, "kdc2.example.test"), new DnsServiceRecord(0, 0, 750, "kdc1.example.test")], DnsLookupFailure.None));
        });
        Diagnostics.Arrange("service name", "_kerberos._udp.EXAMPLE.TEST");
        Diagnostics.Arrange("DNS answer", "10 5 88 kdc2.example.test, 0 0 750 kdc1.example.test");

        IReadOnlyList<KerberosSrvRecord> records = await lookup.LookUpAsync("_kerberos._udp.EXAMPLE.TEST", CancellationToken.None);

        Diagnostics.Act("records", string.Join(", ", records));
        Diagnostics.Act("names looked up", string.Join(", ", names));
        Diagnostics.Assert("record count", 2, records.Count);
        Diagnostics.Assert("names looked up", "_kerberos._udp.EXAMPLE.TEST", string.Join(", ", names));
        CollectionAssert.AreEqual(
            new[] { new KerberosSrvRecord(10, 5, 88, "kdc2.example.test"), new KerberosSrvRecord(0, 0, 750, "kdc1.example.test") },
            records.ToArray());
        CollectionAssert.AreEqual(new[] { "_kerberos._udp.EXAMPLE.TEST" }, names);
    }

    [TestMethod]
    public async Task LookUpAsync_LookupFails_ReturnsNoRecords()
    {
        KerberosDnsSrvLookup lookup = new((_, _) => ValueTask.FromResult(new DnsServiceLookup([], DnsLookupFailure.BadConfiguration)));
        Diagnostics.Arrange("service name", "_kerberos._tcp.EXAMPLE.TEST");
        Diagnostics.Arrange("DNS failure", DnsLookupFailure.BadConfiguration);

        IReadOnlyList<KerberosSrvRecord> records = await lookup.LookUpAsync("_kerberos._tcp.EXAMPLE.TEST", CancellationToken.None);

        Diagnostics.Act("record count", records.Count);
        Diagnostics.Assert("record count", 0, records.Count);
        Assert.IsEmpty(records);
    }
}
