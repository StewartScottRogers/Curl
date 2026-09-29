namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosKdcLocator" /> returns a realm's KDCs from
/// <c>krb5.conf</c> first, and otherwise, when <c>dns_lookup_kdc</c> allows, from the
/// <c>_kerberos._udp</c> and <c>_kerberos._tcp</c> SRV records ordered by priority and
/// weight (RFC 4120 section 7.2.3.2, RFC 2782).
/// </summary>
[TestClass]
public sealed class KerberosKdcLocatorTests
{
    [TestMethod]
    public async Task LocateAsync_RealmHasKdcEntries_ReturnsThemAndSkipsDns()
    {
        FakeSrvLookup dns = new FakeSrvLookup().Add("_kerberos._udp.EXAMPLE.COM", new KerberosSrvRecord(0, 0, 88, "dns.example.com."));
        KerberosKdcLocator locator = new(Parse("[realms]\n EXAMPLE.COM = {\n kdc = kdc1.example.com\n kdc = tcp/kdc2.example.com:750\n }\n"), dns);

        IReadOnlyList<KerberosKdcAddress> kdcs = await locator.LocateAsync("EXAMPLE.COM", CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new KerberosKdcAddress(KerberosKdcTransport.UdpOrTcp, "kdc1.example.com", 88),
                new KerberosKdcAddress(KerberosKdcTransport.Tcp, "kdc2.example.com", 750),
            },
            kdcs.ToArray());
        Assert.IsEmpty(dns.NamesLookedUp);
    }

    [TestMethod]
    public async Task LocateAsync_NoKdcEntries_ReturnsUdpThenTcpSrvRecordsByPriorityThenWeight()
    {
        FakeSrvLookup dns = new FakeSrvLookup()
            .Add(
                "_kerberos._udp.EXAMPLE.COM",
                new KerberosSrvRecord(10, 5, 88, "light.example.com."),
                new KerberosSrvRecord(0, 0, 88, "first.example.com."),
                new KerberosSrvRecord(10, 60, 750, "heavy.example.com"))
            .Add("_kerberos._tcp.EXAMPLE.COM", new KerberosSrvRecord(0, 0, 88, "tcp.example.com."));
        KerberosKdcLocator locator = new(KerberosConfiguration.Empty, dns);

        IReadOnlyList<KerberosKdcAddress> kdcs = await locator.LocateAsync("EXAMPLE.COM", CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                new KerberosKdcAddress(KerberosKdcTransport.Udp, "first.example.com", 88),
                new KerberosKdcAddress(KerberosKdcTransport.Udp, "heavy.example.com", 750),
                new KerberosKdcAddress(KerberosKdcTransport.Udp, "light.example.com", 88),
                new KerberosKdcAddress(KerberosKdcTransport.Tcp, "tcp.example.com", 88),
            },
            kdcs.ToArray());
        CollectionAssert.AreEqual(new[] { "_kerberos._udp.EXAMPLE.COM", "_kerberos._tcp.EXAMPLE.COM" }, dns.NamesLookedUp);
    }

    [TestMethod]
    public async Task LocateAsync_SrvTargetIsDot_MeansNoServiceThere()
    {
        FakeSrvLookup dns = new FakeSrvLookup().Add("_kerberos._udp.EXAMPLE.COM", new KerberosSrvRecord(0, 0, 0, "."));

        IReadOnlyList<KerberosKdcAddress> kdcs = await new KerberosKdcLocator(KerberosConfiguration.Empty, dns).LocateAsync("EXAMPLE.COM", CancellationToken.None);

        Assert.IsEmpty(kdcs);
    }

    [TestMethod]
    public async Task LocateAsync_NoKdcEntriesAndDnsLookupKdcFalse_ReturnsNoneWithoutDns()
    {
        FakeSrvLookup dns = new FakeSrvLookup().Add("_kerberos._udp.EXAMPLE.COM", new KerberosSrvRecord(0, 0, 88, "dns.example.com"));

        IReadOnlyList<KerberosKdcAddress> kdcs = await new KerberosKdcLocator(Parse("[libdefaults]\n dns_lookup_kdc = false\n"), dns).LocateAsync("EXAMPLE.COM", CancellationToken.None);

        Assert.IsEmpty(kdcs);
        Assert.IsEmpty(dns.NamesLookedUp);
    }

    [TestMethod]
    public async Task LocateAsync_MalformedKdcEntry_FailsTheLookup()
    {
        KerberosKdcLocator locator = new(Parse("[realms]\n R = {\n kdc = good\n kdc = bad:port\n }\n"), new FakeSrvLookup());

        KerberosConfigurationException failure = await Assert.ThrowsExactlyAsync<KerberosConfigurationException>(() => locator.LocateAsync("R", CancellationToken.None));

        Assert.AreEqual(KerberosConfigurationError.InvalidKdcAddress, failure.Error);
    }

    [TestMethod]
    [DataRow("kdc.example.com", KerberosKdcTransport.UdpOrTcp, "kdc.example.com", 88, "")]
    [DataRow("kdc.example.com:750", KerberosKdcTransport.UdpOrTcp, "kdc.example.com", 750, "")]
    [DataRow("udp/kdc.example.com", KerberosKdcTransport.Udp, "kdc.example.com", 88, "")]
    [DataRow("tcp/[2001:db8::1]:750", KerberosKdcTransport.Tcp, "2001:db8::1", 750, "")]
    [DataRow("[2001:db8::1]", KerberosKdcTransport.UdpOrTcp, "2001:db8::1", 88, "")]
    [DataRow("2001:db8::1", KerberosKdcTransport.UdpOrTcp, "2001:db8::1", 88, "")]
    [DataRow("a]b:1", KerberosKdcTransport.UdpOrTcp, "a]b", 1, "")]
    [DataRow("https://proxy.example.com/KdcProxy", KerberosKdcTransport.Https, "proxy.example.com", 443, "KdcProxy")]
    [DataRow("HTTPS://proxy.example.com:8443", KerberosKdcTransport.Https, "proxy.example.com", 8443, "")]
    [DataRow("kdc:65535", KerberosKdcTransport.UdpOrTcp, "kdc", 65535, "")]
    public void ParseKdcEntry_ValidEntry_IsParsed(string entry, KerberosKdcTransport transport, string host, int port, string httpsPath)
    {
        Assert.AreEqual(new KerberosKdcAddress(transport, host, port, httpsPath), KerberosKdcLocator.ParseKdcEntry(entry));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(":88")]
    [DataRow("udp/")]
    [DataRow("kdc:")]
    [DataRow("kdc:0")]
    [DataRow("kdc:65536")]
    [DataRow("kdc:+88")]
    [DataRow("kdc:99999999999")]
    [DataRow("[]")]
    [DataRow("[::1]x")]
    [DataRow("https://")]
    public void ParseKdcEntry_MalformedEntry_FailsWithInvalidKdcAddress(string entry)
    {
        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => KerberosKdcLocator.ParseKdcEntry(entry));

        Assert.AreEqual(KerberosConfigurationError.InvalidKdcAddress, failure.Error);
        StringAssert.Contains(failure.Message, entry);
    }

    private static KerberosConfiguration Parse(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "test.conf", root);
        return new KerberosConfiguration(root);
    }
}
