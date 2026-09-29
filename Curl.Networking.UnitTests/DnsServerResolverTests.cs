using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DnsServerResolver" /> through the <see cref="IDnsSocketOpener" /> seam to curl
/// 8.22.0's c-ares 1.34.8 build, measured with <c>Record-CurlExchange.ps1 -DnsPort</c> (BL-694):
/// the query bytes, AAAA and A at once, servers in list order, the timeouts on a fake clock, the
/// TCP retry on truncation, the three binding options, SRV answers and each failure.
/// </summary>
[TestClass]
public sealed class DnsServerResolverTests
{
    private const string Host = "bl694.example";

    private static readonly IPEndPoint First = new(IPAddress.Parse("192.0.2.1"), 53);
    private static readonly IPEndPoint Second = new(IPAddress.Parse("192.0.2.2"), 5353);
    private static readonly IPEndPoint Third = new(IPAddress.Parse("192.0.2.3"), 53);
    private static readonly IPEndPoint SixServer = new(IPAddress.Parse("2001:db8::53"), 53);
    private static readonly IPAddress Four = IPAddress.Parse("198.51.100.7");
    private static readonly IPAddress Six = IPAddress.Parse("2001:db8::7");

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AnAnsweringServer_ReturnsTheAaaaAnswersThenTheAAnswers()
    {
        var opener = Opener((First, ScriptedDnsServer.Answering(Four, Six)));

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.None, resolution.Failure);
        CollectionAssert.AreEqual(new[] { Six, Four }, resolution.Addresses.ToArray());
        CollectionAssert.AreEqual(new[] { DnsRecordType.Aaaa, DnsRecordType.A }, opener.Sent.Select(sent => DnsTestReplies.TypeOf(sent.Query)).ToArray());
        Assert.IsTrue(opener.Sent.All(sent => sent.Transport == "udp" && sent.Server.Equals(First) && sent.LocalAddress.Equals(IPAddress.Any)));
    }

    [TestMethod]
    public async Task ResolveAsync_AnAnsweringServer_ReturnsTheAddresses()
    {
        var opener = Opener((First, ScriptedDnsServer.Answering(Four)));

        var addresses = await Resolver(opener, "192.0.2.1").ResolveAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_SendsTheQueriesTheCaresBuildSent()
    {
        // curl -sS --dns-servers 172.26.96.1:15353 http://bl694.example:1/ (c-ares 1.34.8), dns.txt
        var opener = Opener((First, ScriptedDnsServer.Answering(Four)));
        var random = new QueuedRandom([0x66, 0x04], [0x35, 0x5C, 0x11, 0x4D, 0x1E, 0xDB, 0x1F, 0xDF], [0x9D, 0x89]);

        await Resolver(opener, "192.0.2.1", fillRandom: random.Fill).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(
            "66040100000100000000000105626C363934076578616D706C6500001C000100002904D000000000000C000A0008355C114D1EDB1FDF",
            Convert.ToHexString(opener.Sent[0].Query));
        Assert.AreEqual(
            "9D890100000100000000000105626C363934076578616D706C65000001000100002904D000000000000C000A0008355C114D1EDB1FDF",
            Convert.ToHexString(opener.Sent[1].Query));
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ATruncatedReply_AsksAgainOverTcpWithoutTheCookie()
    {
        // The -DnsTruncate measurement: each query followed at once over TCP, same ID, empty OPT.
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [], truncated: true),
            AnswerOverTcp = query => DnsTestReplies.Answer(query, [Four]),
        };
        var opener = Opener((First, server));
        var random = new QueuedRandom([0x83, 0x2A], [0x42, 0x37, 0x3E, 0xA2, 0x36, 0x5E, 0x5B, 0x2B]);

        var resolution = await Resolver(opener, "192.0.2.1", AddressFamily.InterNetworkV6, fillRandom: random.Fill)
            .ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.NoData, resolution.Failure);
        CollectionAssert.AreEqual(new[] { "udp", "tcp" }, opener.Sent.Select(sent => sent.Transport).ToArray());
        Assert.AreEqual(
            "832A0100000100000000000105626C363934076578616D706C6500001C000100002904D0000000000000",
            Convert.ToHexString(opener.Sent[1].Query));
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ATruncatedReply_ReturnsTheTcpAnswer()
    {
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [], truncated: true),
            AnswerOverTcp = query => DnsTestReplies.Answer(query, [Four]),
        };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ATcpReplyForAnotherQuery_IsABadReply()
    {
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [], truncated: true),
            AnswerOverTcp = query => DnsTestReplies.Answer([.. query.Take(1), (byte)(query[1] ^ 0xFF), .. query.Skip(2)], [Four]),
        };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.BadReply, resolution.Failure);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ATcpConnectionClosedUnanswered_CouldNotContactTheServer()
    {
        var server = new ScriptedDnsServer { AnswerOverUdp = query => DnsTestReplies.Answer(query, [], truncated: true) };
        var opener = Opener((First, server));

        var resolution = await Resolver(opener, "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.Unreachable, resolution.Failure);
        Assert.AreEqual(DnsServerResolver.Rounds * 2, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ASilentFirstServer_AsksTheSecondAfterTwoSeconds()
    {
        // The order-silent-first measurement: both queries to the first server at once, then to the second 2000 ms later.
        var time = new TimerCountingTimeProvider();
        var opener = Opener(time, (Second, ScriptedDnsServer.Answering(Four, Six)));

        var resolution = await RunOutTimeoutsAsync(Resolver(opener, "192.0.2.1,192.0.2.2:5353", time: time).ResolveWithFailureReasonAsync(Host, CancellationToken.None).AsTask(), time, opener, 2);

        CollectionAssert.AreEqual(new[] { Six, Four }, resolution.Addresses.ToArray());
        CollectionAssert.AreEqual(new[] { First, First, Second, Second }, opener.Sent.Select(sent => sent.Server).ToArray());
        CollectionAssert.AreEqual(new long[] { 0, 0, 2000, 2000 }, opener.Sent.Select(sent => sent.SentAt).ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_OneSilentServer_TriesThreeRoundsDoublingTheWaitThenTimesOut()
    {
        var time = new TimerCountingTimeProvider();
        var opener = Opener(time);

        var resolution = await RunOutTimeoutsAsync(Resolver(opener, "192.0.2.1", AddressFamily.InterNetwork, time: time).ResolveWithFailureReasonAsync(Host, CancellationToken.None).AsTask(), time, opener, 1);

        Assert.AreEqual(DnsLookupFailure.Timeout, resolution.Failure);
        CollectionAssert.AreEqual(new long[] { 0, 2000, 6000 }, opener.Sent.Select(sent => sent.SentAt).ToArray());
        Assert.AreEqual(14000, time.GetTimestamp());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ThreeSilentServers_GoesRoundTheListThreeTimes()
    {
        // The silent3 measurement: 0, 2000 and 4000 ms, then each server again, round after round.
        var time = new TimerCountingTimeProvider();
        var opener = Opener(time);

        var resolution = await RunOutTimeoutsAsync(Resolver(opener, "192.0.2.1, 192.0.2.2:5353 ,192.0.2.3", AddressFamily.InterNetwork, time: time).ResolveWithFailureReasonAsync(Host, CancellationToken.None).AsTask(), time, opener, 1);

        Assert.AreEqual(DnsLookupFailure.Timeout, resolution.Failure);
        CollectionAssert.AreEqual(
            new[] { First, Second, Third, First, Second, Third, First, Second, Third },
            opener.Sent.Select(sent => sent.Server).ToArray());
        CollectionAssert.AreEqual(
            new long[] { 0, 2000, 4000, 6000, 10000, 14000, 18000, 26000, 34000 },
            opener.Sent.Select(sent => sent.SentAt).ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_NxDomain_EndsTheQueryAsNotFound()
    {
        var opener = Opener((First, ScriptedDnsServer.AnsweringCode(3)), (Second, ScriptedDnsServer.Answering(Four)));

        var resolution = await Resolver(opener, "192.0.2.1,192.0.2.2:5353").ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.NotFound, resolution.Failure);
        Assert.AreEqual(2, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_NoRecordOfTheType_EndsTheQueryAsNoData()
    {
        var opener = Opener((First, ScriptedDnsServer.Answering(Six)));

        var resolution = await Resolver(opener, "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.NoData, resolution.Failure);
        Assert.AreEqual(1, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ServFail_TriesEveryRoundThenReportsAGeneralFailure()
    {
        // The servfail measurement: three queries of each type, then "DNS server returned general failure".
        var opener = Opener((First, ScriptedDnsServer.AnsweringCode(2)));

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.ServerFailure, resolution.Failure);
        Assert.AreEqual(6, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ServFailThenAnAnswer_ReturnsTheSecondServersAnswer()
    {
        var opener = Opener((First, ScriptedDnsServer.AnsweringCode(2)), (Second, ScriptedDnsServer.Answering(Four)));

        var resolution = await Resolver(opener, "192.0.2.1,192.0.2.2:5353", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ASocketThatCannotBeOpened_CouldNotContactTheServers()
    {
        var opener = Opener((First, new ScriptedDnsServer { OpenFailure = new SocketException((int)SocketError.AddressNotAvailable) }));

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.Unreachable, resolution.Failure);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_APortUnreachable_CouldNotContactTheServers()
    {
        var opener = Opener((First, new ScriptedDnsServer { ReceiveFailure = new SocketException((int)SocketError.ConnectionReset) }));

        var resolution = await Resolver(opener, "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.Unreachable, resolution.Failure);
        Assert.AreEqual(DnsServerResolver.Rounds, opener.Sent.Count);
    }

    [TestMethod]
    [DataRow("198.51.100.1", 53, DisplayName = "another address, the server's port")]
    [DataRow("192.0.2.1", 9999, DisplayName = "the server's address, another port")]
    public async Task ResolveWithFailureReasonAsync_AReplyFromAnotherEndPoint_IsIgnored(string address, int port)
    {
        var elsewhere = IPAddress.Parse("203.0.113.9");
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [Four]),
            StrayDatagram = query => (new IPEndPoint(IPAddress.Parse(address), port), DnsTestReplies.Answer(query, [elsewhere])),
        };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ADatagramFromAnEndPointThatIsNotAnAddress_IsIgnored()
    {
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [Four]),
            StrayDatagram = query => (new DnsEndPoint("elsewhere.example", 53), DnsTestReplies.Answer(query, [IPAddress.Loopback])),
        };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AReplyWhoseSourceCarriesAScopeId_IsTheServers()
    {
        var linkLocal = new IPEndPoint(IPAddress.Parse("fe80::53"), 53);
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [Six]),
            ReplySource = new IPEndPoint(IPAddress.Parse("fe80::53%5"), 53),
        };

        var resolution = await Resolver(Opener((linkLocal, server)), "fe80::53", AddressFamily.InterNetworkV6).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Six }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AReplyForAnotherQueryFromTheServer_IsIgnored()
    {
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.Answer(query, [Four]),
            StrayDatagram = _ => (First, new byte[5]),
        };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AReplyThatDoesNotDecode_IsABadReply()
    {
        var server = new ScriptedDnsServer { AnswerOverUdp = query => [.. DnsTestReplies.Answer(query, [Four]), 0xFF] };

        var resolution = await Resolver(Opener((First, server)), "192.0.2.1", AddressFamily.InterNetwork).ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.BadReply, resolution.Failure);
    }

    [TestMethod]
    [DataRow("bogus", null, null)]
    [DataRow("192.0.2.1:99999", null, null)]
    [DataRow("192.0.2.1", "bogus", null)]
    [DataRow("192.0.2.1", null, "192.0.2.9")]
    public async Task ResolveWithFailureReasonAsync_AnOptionThatDoesNotParse_IsABadConfigurationAndSendsNothing(string servers, string? ipv4Address, string? ipv6Address)
    {
        var opener = Opener();
        var resolver = new DnsServerResolver(new DnsServerResolverOptions(servers, null, ipv4Address, ipv6Address), new ManualTimeProvider(), opener, () => [], _ => null, new QueuedRandom().Fill);

        var resolution = await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.BadConfiguration, resolution.Failure);
        Assert.AreEqual(0, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_TheBindingOptions_BindEachFamilysSocket()
    {
        var sixAnswering = ScriptedDnsServer.Answering(Six);
        var opener = Opener((First, ScriptedDnsServer.AnsweringCode(2)), (SixServer, sixAnswering));
        var ipv4 = IPAddress.Parse("10.1.2.3");
        var ipv6 = IPAddress.Parse("fd00::3");
        var resolver = new DnsServerResolver(
            new DnsServerResolverOptions("192.0.2.1,[2001:db8::53]", null, "10.1.2.3", "fd00::3", AddressFamily.InterNetworkV6),
            new ManualTimeProvider(),
            opener,
            () => [],
            _ => null,
            new QueuedRandom().Fill);

        await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(ipv4, opener.Sent[0].LocalAddress);
        Assert.AreEqual(ipv6, opener.Sent[1].LocalAddress);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AnInterfaceName_BindsItsAddressOfTheServersFamily()
    {
        var opener = Opener((First, ScriptedDnsServer.Answering(Four)));
        var eth0 = IPAddress.Parse("172.26.99.197");
        var resolver = new DnsServerResolver(
            new DnsServerResolverOptions("192.0.2.1", "eth0", null, null, AddressFamily.InterNetwork),
            new ManualTimeProvider(),
            opener,
            () => [],
            name => name == "eth0" ? [IPAddress.Parse("fe80::1"), eth0] : null,
            new QueuedRandom().Fill);

        await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(eth0, opener.Sent[0].LocalAddress);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_AnInterfaceThatDoesNotExist_LeavesTheSocketOnAnyAddress()
    {
        // The iface-bad measurement: --dns-interface nosuchif0 still resolves.
        var opener = Opener((First, ScriptedDnsServer.Answering(Four)));
        var resolver = new DnsServerResolver(
            new DnsServerResolverOptions("192.0.2.1", "nosuchif0", null, null, AddressFamily.InterNetwork),
            new ManualTimeProvider(),
            opener,
            () => [],
            _ => null,
            new QueuedRandom().Fill);

        var resolution = await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
        Assert.AreEqual(IPAddress.Any, opener.Sent[0].LocalAddress);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_WithoutAServerList_AsksTheSystemsServers()
    {
        var opener = Opener((Third, ScriptedDnsServer.Answering(Four)));
        var resolver = new DnsServerResolver(
            new DnsServerResolverOptions(null, null, "10.1.2.3", null, AddressFamily.InterNetwork),
            new ManualTimeProvider(),
            opener,
            () => [Third],
            _ => null,
            new QueuedRandom().Fill);

        var resolution = await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Four }, resolution.Addresses.ToArray());
        Assert.AreEqual(Third, opener.Sent[0].Server);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_NoSystemServers_CouldNotContactTheServers()
    {
        var opener = Opener();
        var resolver = new DnsServerResolver(new DnsServerResolverOptions(null, "eth0", null, null), new ManualTimeProvider(), opener, () => [], _ => null, new QueuedRandom().Fill);

        var resolution = await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.Unreachable, resolution.Failure);
        Assert.AreEqual(0, opener.Sent.Count);
    }

    [TestMethod]
    [DataRow("192.0.2.44")]
    [DataRow("2001:db8::44")]
    public async Task ResolveWithFailureReasonAsync_AnAddressLiteral_ReturnsItWithoutAQuery(string literal)
    {
        var opener = Opener();

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync(literal, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse(literal) }, resolution.Addresses.ToArray());
        Assert.AreEqual(0, opener.Sent.Count);
    }

    [TestMethod]
    [DataRow("localhost")]
    [DataRow("LOCALHOST")]
    [DataRow("app.localhost")]
    public async Task ResolveWithFailureReasonAsync_Localhost_ReturnsBothLoopbacksWithoutAQuery(string host)
    {
        var opener = Opener();

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync(host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, resolution.Addresses.ToArray());
        Assert.AreEqual(0, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_ANameThatCannotBeQueried_IsABadNameAndSendsNothing()
    {
        var opener = Opener();

        var resolution = await Resolver(opener, "192.0.2.1").ResolveWithFailureReasonAsync("a..b", CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.BadName, resolution.Failure);
        Assert.AreEqual(0, opener.Sent.Count);
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_Cancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        var lookup = Resolver(Opener(), "192.0.2.1").ResolveWithFailureReasonAsync(Host, cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => lookup);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(" ")]
    public async Task ResolveWithFailureReasonAsync_WithoutAHost_ThrowsArgumentException(string? host)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Resolver(Opener(), "192.0.2.1").ResolveWithFailureReasonAsync(host!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task ResolveWithFailureReasonAsync_TwoLookups_SendTheSameCookieToTheSameServer()
    {
        var opener = Opener((First, ScriptedDnsServer.Answering(Four)));
        var resolver = Resolver(opener, "192.0.2.1", AddressFamily.InterNetwork, fillRandom: new QueuedRandom([0, 1], [1, 2, 3, 4, 5, 6, 7, 8], [0, 2]).Fill);

        await resolver.ResolveWithFailureReasonAsync(Host, CancellationToken.None);
        await resolver.ResolveWithFailureReasonAsync("other.example", CancellationToken.None);

        CollectionAssert.AreEqual(opener.Sent[0].Query[^8..], opener.Sent[1].Query[^8..]);
        CollectionAssert.AreNotEqual(opener.Sent[0].Query[..2], opener.Sent[1].Query[..2]);
    }

    [TestMethod]
    public async Task ResolveServiceAsync_AnAnsweringServer_ReturnsTheSrvRecords()
    {
        var server = new ScriptedDnsServer
        {
            AnswerOverUdp = query => DnsTestReplies.AnswerServices(query, (0, 100, 88, "kdc1.example.com"), (10, 5, 750, "kdc2.example.com")),
        };
        var opener = Opener((First, server));

        var lookup = await Resolver(opener, "192.0.2.1").ResolveServiceAsync("_kerberos._udp.EXAMPLE.COM", CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.None, lookup.Failure);
        CollectionAssert.AreEqual(
            new[] { new DnsServiceRecord(0, 100, 88, "kdc1.example.com"), new DnsServiceRecord(10, 5, 750, "kdc2.example.com") },
            lookup.Records.ToArray());
        Assert.AreEqual(DnsRecordType.Srv, DnsTestReplies.TypeOf(opener.Sent.Single().Query));
    }

    [TestMethod]
    public async Task ResolveServiceAsync_NxDomain_ReturnsNoRecordsAndNotFound()
    {
        var lookup = await Resolver(Opener((First, ScriptedDnsServer.AnsweringCode(3))), "192.0.2.1").ResolveServiceAsync("_kerberos._udp.EXAMPLE.COM", CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.NotFound, lookup.Failure);
        Assert.AreEqual(0, lookup.Records.Count);
    }

    [TestMethod]
    public async Task ResolveServiceAsync_AListThatDoesNotParse_IsABadConfiguration()
    {
        var lookup = await Resolver(Opener(), "bogus").ResolveServiceAsync("_kerberos._udp.EXAMPLE.COM", CancellationToken.None);

        Assert.AreEqual(DnsLookupFailure.BadConfiguration, lookup.Failure);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task ResolveServiceAsync_WithoutAName_ThrowsArgumentException(string? name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Resolver(Opener(), "192.0.2.1").ResolveServiceAsync(name!, CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Fires each attempt's timeout on <paramref name="time" /> once the <paramref name="concurrentQueries" />
    /// queries are all waiting, until <paramref name="lookup" /> ends. An attempt arms its timeout
    /// before it sends, so the clock moves only once <paramref name="opener" /> has recorded a send
    /// for every timer armed; moving it between the two would stamp the send late.
    /// </summary>
    private static async Task<T> RunOutTimeoutsAsync<T>(Task<T> lookup, TimerCountingTimeProvider time, ScriptedDnsSocketOpener opener, int concurrentQueries)
    {
        while (true)
        {
            var clock = Stopwatch.StartNew();
            while (!lookup.IsCompleted && (time.Clock.PendingTimerCount < concurrentQueries || opener.Sent.Count < time.TimersCreated))
            {
                Assert.IsLessThan(10000L, clock.ElapsedMilliseconds, "The lookup neither finished nor waited.");
                await Task.Yield();
            }

            if (lookup.IsCompleted)
            {
                return await lookup;
            }

            time.Clock.Advance(time.Clock.NextDueAt!.Value - time.GetTimestamp());
        }
    }

    private static ScriptedDnsSocketOpener Opener(params (IPEndPoint EndPoint, ScriptedDnsServer Server)[] servers) =>
        new(new ManualTimeProvider(), servers);

    private static ScriptedDnsSocketOpener Opener(TimeProvider time, params (IPEndPoint EndPoint, ScriptedDnsServer Server)[] servers) =>
        new(time, servers);

    private static DnsServerResolver Resolver(
        ScriptedDnsSocketOpener opener,
        string servers,
        AddressFamily addressFamily = AddressFamily.Unspecified,
        TimeProvider? time = null,
        Action<Span<byte>>? fillRandom = null) =>
        new(
            new DnsServerResolverOptions(servers, null, null, null, addressFamily),
            time ?? new ManualTimeProvider(),
            opener,
            () => [],
            _ => null,
            fillRandom ?? new QueuedRandom().Fill);

    /// <summary>A <see cref="ManualTimeProvider" /> that also counts every timer created on it, fired or not.</summary>
    private sealed class TimerCountingTimeProvider : TimeProvider
    {
        private int _timersCreated;

        public ManualTimeProvider Clock { get; } = new();

        public int TimersCreated => Volatile.Read(ref _timersCreated);

        public override long TimestampFrequency => Clock.TimestampFrequency;

        public override long GetTimestamp() => Clock.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref _timersCreated);
            return Clock.CreateTimer(callback, state, dueTime, period);
        }
    }

    /// <summary>Fills each request with the next queued bytes, then with a counter once the queue is empty.</summary>
    private sealed class QueuedRandom(params byte[][] fills)
    {
        private readonly Queue<byte[]> _fills = new(fills);
        private readonly Lock _lock = new();
        private byte _counter;

        public void Fill(Span<byte> destination)
        {
            lock (_lock)
            {
                if (_fills.TryDequeue(out var next))
                {
                    next.CopyTo(destination);
                    return;
                }

                for (var index = 0; index < destination.Length; index++)
                {
                    destination[index] = ++_counter;
                }
            }
        }
    }
}
