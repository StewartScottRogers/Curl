using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="DohDnsResolver" /> to curl 8.21.0's DNS-over-HTTPS resolve as ADR-0152 and
/// BL-641 measured it with <c>Record-CurlExchange.ps1 -Tls</c> standing in for the DoH server:
/// the two POSTs byte for byte, the addresses of A and AAAA answers, and exit 6 for a <c>500</c>,
/// an NXDOMAIN and a malformed answer. The DoH connections come from a <see cref="FakeConnector" />,
/// or from a <see cref="TcpConnector" /> over a <see cref="FakeTcpDialer" /> and
/// <see cref="FakeTlsProvider" />, so no test touches the network.
/// </summary>
[TestClass]
public sealed class DohDnsResolverTests
{
    private const int DohPort = 48711;

    private static readonly Uri MeasuredDohUrl = new($"https://127.0.0.1:{DohPort}/dns-query");

    private static readonly IPAddress IPv6Address = IPAddress.Parse("2001:db8::1");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new DohDnsResolver(null!, MeasuredDohUrl));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "connector", exception.ParamName);
        Assert.AreEqual("connector", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullUrl_Throws()
    {
        Diagnostics.Arrange("DoH URL", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new DohDnsResolver(new FakeConnector(), null!));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "dohUrl", exception.ParamName);
        Assert.AreEqual("dohUrl", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithRelativeUrl_Throws()
    {
        Diagnostics.Arrange("DoH URL", "/dns-query (relative)");

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new DohDnsResolver(new FakeConnector(), new Uri("/dns-query", UriKind.Relative)));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception parameter", "dohUrl", exception.ParamName);
        Assert.AreEqual("dohUrl", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ResolveAsync_WithNoHost_Throws(string? host)
    {
        Diagnostics.Arrange("host", host is null ? "null" : $"\"{host}\"");
        var resolver = new DohDnsResolver(new FakeConnector(), MeasuredDohUrl);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await resolver.ResolveAsync(host!, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception is an ArgumentException", true, exception is not null);
    }

    [TestMethod]
    public async Task ResolveAsync_ThePostsCurlSent_AreWrittenByteForByte()
    {
        // ADR-0152: curl 8.21.0 POSTed this for example.test's A query, and the same with
        // QTYPE 00 1C for AAAA, each on a connection of its own, A first.
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "example.test");

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        var head = "POST /dns-query HTTP/1.1\r\n"
            + $"Host: 127.0.0.1:{DohPort}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + "Content-Length: 30\r\n"
            + "\r\n";
        var queryA = Convert.FromHexString("000001000001000000000000076578616D706C650474657374000001" + "0001");
        var queryAaaa = Convert.FromHexString("000001000001000000000000076578616D706C65047465737400001C" + "0001");
        Diagnostics.Assert("connections opened", 2, connector.Opened.Count);
        Assert.HasCount(2, connector.Opened);
        Diagnostics.Diff("A POST", Encoding.Latin1.GetBytes(head).Concat(queryA).ToArray(), connector.Opened[0].Written.ToArray());
        Diagnostics.Diff("AAAA POST", Encoding.Latin1.GetBytes(head).Concat(queryAaaa).ToArray(), connector.Opened[1].Written.ToArray());
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(queryA).ToArray(), connector.Opened[0].Written);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(queryAaaa).ToArray(), connector.Opened[1].Written);
        var everyConnectionFlushedOnceAndDisposed = connector.Opened.All(connection => connection.FlushCount == 1 && connection.IsDisposed);
        Diagnostics.Assert("every connection flushed once and disposed", true, everyConnectionFlushedOnceAndDisposed);
        Assert.IsTrue(everyConnectionFlushedOnceAndDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_ConnectsToTheDohServerOverTlsOfferingHttp11()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "example.test");

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteTargets(connector);
        var target = new ConnectTarget("127.0.0.1", DohPort, UseTls: true) { PoolScheme = "https" };
        var protocols = TcpConnector.ApplicationProtocolsFor(connector.Targets[0], ["http/1.1"]).ToArray();
        Diagnostics.Act("ALPN offered", string.Join(",", protocols));
        Diagnostics.Assert("targets", $"{target}, {target}", string.Join(", ", connector.Targets));
        Diagnostics.Assert("ALPN offered", "http/1.1", string.Join(",", protocols));
        CollectionAssert.AreEqual(new[] { target, target }, connector.Targets);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, protocols);
    }

    [TestMethod]
    public async Task ResolveAsync_WithTheSchemesDefaultPort_WritesHostWithoutAPortAndKeepsTheQuery()
    {
        var connector = new FakeConnector();
        var dohUrl = new Uri("https://dns.example/dns-query?ct=1");
        var resolver = new DohDnsResolver(connector, dohUrl);
        ArrangeResolve(dohUrl, "example.test");

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        WriteTargets(connector);
        var written = Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray());
        var expectedTarget = new ConnectTarget("dns.example", 443, UseTls: true) { PoolScheme = "https" };
        Diagnostics.Assert("head starts with the query and a port-less Host", true, written.StartsWith("POST /dns-query?ct=1 HTTP/1.1\r\nHost: dns.example\r\nAccept: */*\r\n", StringComparison.Ordinal));
        Diagnostics.Assert("first target", expectedTarget, connector.Targets[0]);
        StringAssert.StartsWith(written, "POST /dns-query?ct=1 HTTP/1.1\r\nHost: dns.example\r\nAccept: */*\r\n");
        Assert.AreEqual(expectedTarget, connector.Targets[0]);
    }

    [TestMethod]
    public async Task ResolveAsync_WithAnIPv6DohServer_BracketsItInHostAndConnectsWithout()
    {
        var connector = new FakeConnector();
        var dohUrl = new Uri("https://[::1]:8443/q");
        var resolver = new DohDnsResolver(connector, dohUrl);
        ArrangeResolve(dohUrl, "example.test");

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        WriteTargets(connector);
        var written = Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray());
        Diagnostics.Assert("head holds a bracketed Host", true, written.Contains("\r\nHost: [::1]:8443\r\n", StringComparison.Ordinal));
        Diagnostics.Assert("first target's host", "::1", connector.Targets[0].Host);
        StringAssert.Contains(written, "\r\nHost: [::1]:8443\r\n");
        Assert.AreEqual("::1", connector.Targets[0].Host);
    }

    [TestMethod]
    public async Task ResolveAsync_WithAnHttpDohServer_ConnectsWithoutTls()
    {
        var connector = new FakeConnector();
        var dohUrl = new Uri("http://dns.example/dns-query");
        var resolver = new DohDnsResolver(connector, dohUrl);
        ArrangeResolve(dohUrl, "example.test");

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        WriteTargets(connector);
        var expectedTarget = new ConnectTarget("dns.example", 80, UseTls: false) { PoolScheme = "http" };
        var written = Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray());
        Diagnostics.Assert("first target", expectedTarget, connector.Targets[0]);
        Diagnostics.Assert("head holds a port-less Host", true, written.Contains("\r\nHost: dns.example\r\n", StringComparison.Ordinal));
        Assert.AreEqual(expectedTarget, connector.Targets[0]);
        StringAssert.Contains(written, "\r\nHost: dns.example\r\n");
    }

    [TestMethod]
    public async Task ResolveAsync_TheMeasuredAAnswerAndAnEmptyAaaaAnswer_ReturnsTheIPv4Address()
    {
        // ADR-0152: A answered 127.0.0.1 TTL 60, AAAA NOERROR with no record; curl printed
        // "IPv6: (none)" and "IPv4: 127.0.0.1".
        var connector = Answering(Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback)), Ok(AnswerTo(DnsRecordType.Aaaa, IPAddress.Loopback)));
        ArrangeResolve(MeasuredDohUrl, "example.test");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([IPAddress.Loopback], addresses);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, (byte)0x01)]
    [DataRow(AddressFamily.InterNetworkV6, (byte)0x1C)]
    public async Task ResolveAsync_WithOneAddressFamily_PostsOnlyThatFamilysQuery(AddressFamily family, byte queryType)
    {
        // BL-642: curl 8.21.0 -4 sent one POST with QTYPE 00 01, and -6 one with QTYPE 00 1C.
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl) { AddressFamily = family };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("address family", family);

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        Diagnostics.Assert("connections opened", 1, connector.Opened.Count);
        Assert.HasCount(1, connector.Opened);
        byte[] written = [.. connector.Opened[0].Written];
        Diagnostics.Diff("QTYPE and QCLASS", new byte[] { 0x00, queryType, 0x00, 0x01 }, written[^4..]);
        CollectionAssert.AreEqual(new byte[] { 0x00, queryType, 0x00, 0x01 }, written[^4..]);
    }

    [TestMethod]
    public async Task ResolveAsync_WithIpv6Only_ReturnsTheAaaaAnswersAddresses()
    {
        var connector = Answering(Ok(AnswerTo(DnsRecordType.Aaaa, IPv6Address)));
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl) { AddressFamily = AddressFamily.InterNetworkV6 };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("address family", AddressFamily.InterNetworkV6);

        var addresses = await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([IPv6Address], addresses);
        CollectionAssert.AreEqual(new[] { IPv6Address }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_WithUnspecifiedAddressFamily_PostsBothQueriesAFirst()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl) { AddressFamily = AddressFamily.Unspecified };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("address family", AddressFamily.Unspecified);

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        WriteSent(connector);
        var defaultFamily = new DohDnsResolver(connector, MeasuredDohUrl).AddressFamily;
        Diagnostics.Assert("default address family", AddressFamily.Unspecified, defaultFamily);
        Diagnostics.Assert("connections opened", 2, connector.Opened.Count);
        Assert.AreEqual(AddressFamily.Unspecified, defaultFamily);
        Assert.HasCount(2, connector.Opened);
        Diagnostics.Assert("first QTYPE", (byte)0x01, connector.Opened[0].Written[^3]);
        Diagnostics.Assert("second QTYPE", (byte)0x1C, connector.Opened[1].Written[^3]);
        Assert.AreEqual((byte)0x01, connector.Opened[0].Written[^3]);
        Assert.AreEqual((byte)0x1C, connector.Opened[1].Written[^3]);
    }

    [TestMethod]
    public async Task ResolveAsync_AAndAaaaAnswers_ReturnsTheIPv6AddressesFirst()
    {
        var connector = Answering(
            Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback, IPAddress.Parse("192.0.2.1"))),
            Ok(AnswerTo(DnsRecordType.Aaaa, IPv6Address)));
        ArrangeResolve(MeasuredDohUrl, "example.test");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([IPv6Address, IPAddress.Loopback, IPAddress.Parse("192.0.2.1")], addresses);
        CollectionAssert.AreEqual(new[] { IPv6Address, IPAddress.Loopback, IPAddress.Parse("192.0.2.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AnAnswerWithStatus500_IsStillDecoded()
    {
        // Measured on 2026-09-28 (BL-641): a 500 carrying a valid A answer resolved the name,
        // curl printing "IPv4: 127.0.0.1" and going on to connect (exit 7).
        var answer = AnswerTo(DnsRecordType.A, IPAddress.Loopback);
        var response = Encoding.Latin1.GetBytes($"HTTP/1.1 500 Internal Server Error\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length}\r\n\r\n");
        var connector = Answering([.. response, .. answer]);
        ArrangeResolve(MeasuredDohUrl, "example.test");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([IPAddress.Loopback], addresses);
        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AnIPAddressLiteral_IsReturnedWithoutAQuery()
    {
        // curl -v --doh-url ... http://127.0.0.2:P/ and http://[::1]:P/ asked no DoH server (measured).
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "127.0.0.2 then [::1]");

        var ipv4 = await resolver.ResolveAsync("127.0.0.2", CancellationToken.None);
        var ipv6 = await resolver.ResolveAsync("[::1]", CancellationToken.None);

        WriteAddresses([IPAddress.Parse("127.0.0.2")], ipv4);
        WriteAddresses([IPAddress.IPv6Loopback], ipv6);
        Diagnostics.Assert("DoH connections asked for", 0, connector.Targets.Count);
        CollectionAssert.AreEqual(new[] { IPAddress.Parse("127.0.0.2") }, ipv4.ToArray());
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback }, ipv6.ToArray());
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    [DataRow("localhost")]
    [DataRow("a.LOCALHOST")]
    public async Task ResolveAsync_Localhost_IsAnsweredWithoutAQuery(string host)
    {
        // curl printed "IPv6: ::1" and "IPv4: 127.0.0.1" with no DoH server listening (measured).
        var connector = new FakeConnector();
        ArrangeResolve(MeasuredDohUrl, host);

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync(host, CancellationToken.None);

        WriteAddresses([IPAddress.IPv6Loopback, IPAddress.Loopback], addresses);
        Diagnostics.Assert("DoH connections asked for", 0, connector.Targets.Count);
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_AHostTheEncoderRefuses_SendsNoQuery()
    {
        var connector = new FakeConnector();
        ArrangeResolve(MeasuredDohUrl, "a..test");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("a..test", CancellationToken.None);

        WriteAddresses([], addresses);
        Diagnostics.Assert("DoH connections asked for", 0, connector.Targets.Count);
        Assert.IsEmpty(addresses);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheDohServerCannotBeReached_ReturnsNoAddress()
    {
        // curl gave exit 6 after about 2 s with nothing listening on the DoH port (ADR-0152).
        var connector = new FakeConnector { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect") };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("connect failure", "CouldntConnect, Failed to connect");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([], addresses);
        WriteTargets(connector);
        Diagnostics.Assert("DoH connections asked for", 2, connector.Targets.Count);
        Assert.IsEmpty(addresses);
        Assert.HasCount(2, connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheConnectionFailsMidExchange_ReturnsNoAddressAndDisposesIt()
    {
        var connector = new FakeConnector { ReadException = new IOException("reset") };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("read exception", "IOException: reset");

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        WriteAddresses([], addresses);
        var everyConnectionDisposed = connector.Opened.All(connection => connection.IsDisposed);
        Diagnostics.Assert("every connection disposed", true, everyConnectionDisposed);
        Assert.IsEmpty(addresses);
        Assert.IsTrue(everyConnectionDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenCancelled_Throws()
    {
        var connector = new FakeConnector { ReadException = new OperationCanceledException() };
        ArrangeResolve(MeasuredDohUrl, "example.test");
        Diagnostics.Arrange("read exception", nameof(OperationCanceledException));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception is an OperationCanceledException", true, exception is not null);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenBothDohAnswersAre500WithNoBody_FailsWithExit6()
    {
        // ADR-0152: "500, empty body, both connections" -> curl: (6) Could not resolve host: example.test.
        var connector = Answering(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"), Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));
        Diagnostics.Arrange("DoH answers", "500 with no body, twice");

        await AssertCouldNotResolveAsync(connector);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenBothDohAnswersAreNxdomain_FailsWithExit6()
    {
        // ADR-0152: RCODE 3 in both answers -> "DoH: Bad RCODE type A ...", exit 6.
        var connector = Answering(
            Ok(DnsTestReplies.Answer(QueryFor(DnsRecordType.A), [IPAddress.Loopback], responseCode: 3)),
            Ok(DnsTestReplies.Answer(QueryFor(DnsRecordType.Aaaa), [IPv6Address], responseCode: 3)));
        Diagnostics.Arrange("DoH answers", "RCODE 3 (NXDOMAIN) for A and AAAA");

        await AssertCouldNotResolveAsync(connector);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenTheAAnswerIsTruncatedAndTheAaaaAnswerEmpty_FailsWithExit6()
    {
        // ADR-0152: a 45-byte Content-Length on the 46-byte A answer -> "DoH: Out of range type A",
        // and the AAAA answer with no record -> "DoH: No content type AAAA", exit 6.
        var answer = AnswerTo(DnsRecordType.A, IPAddress.Loopback);
        var truncated = Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length - 1}\r\n\r\n")
            .Concat(answer).ToArray();
        var connector = Answering(truncated, Ok(AnswerTo(DnsRecordType.Aaaa, IPAddress.Loopback)));
        Diagnostics.Arrange("DoH answers", $"A with Content-Length {answer.Length - 1} of {answer.Length} bytes, AAAA with no record");
        Diagnostics.Bytes("A answer", truncated);

        await AssertCouldNotResolveAsync(connector);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenTheDohServersCertificateIsRefused_FailsWithExit6()
    {
        // ADR-0152: without --doh-insecure both handshakes failed ("DoH request SSL peer
        // certificate or SSH remote key was not OK") and curl gave exit 6.
        var dohDialer = new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) };
        var dohTls = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "SSL peer certificate or SSH remote key was not OK") };
        var dohConnector = new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), dohDialer, dohTls, new ManualTimeProvider());
        Diagnostics.Arrange("DoH handshake failure", "PeerFailedVerification, SSL peer certificate or SSH remote key was not OK");

        await AssertCouldNotResolveAsync(dohConnector);

        Diagnostics.Act("DoH handshakes", dohTls.HandshakeCount);
        Diagnostics.Assert("DoH handshakes", 2, dohTls.HandshakeCount);
        Assert.AreEqual(2, dohTls.HandshakeCount);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenTheDohServerAnswers_DialsTheResolvedAddress()
    {
        // ADR-0152: the A answer 127.0.0.1 -> "Host example.test:48637 was resolved." and
        // "Trying 127.0.0.1:48637...".
        var dohConnector = Answering(Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback)), Ok(AnswerTo(DnsRecordType.Aaaa, IPAddress.Loopback)));
        var dialer = new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) };
        var connector = new TcpConnector(new DohDnsResolver(dohConnector, MeasuredDohUrl), dialer, new FakeTlsProvider(), new ManualTimeProvider());
        var target = new ConnectTarget("example.test", 48637, UseTls: false);
        Diagnostics.Arrange("target", target);
        Diagnostics.Arrange("DoH answers", "A 127.0.0.1, AAAA with no record");

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Diagnostics.Act("connected", result.Connection is not null);
        Diagnostics.Act("dialed", string.Join(", ", dialer.DialedEndPoints));
        Diagnostics.Assert("dialed", "127.0.0.1:48637", string.Join(", ", dialer.DialedEndPoints));
        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 48637) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ResolveHttpsRecordAsync_WithNoHost_Throws(string? host)
    {
        Diagnostics.Arrange("host", host is null ? "null" : $"\"{host}\"");
        var resolver = new DohDnsResolver(new FakeConnector(), MeasuredDohUrl);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await resolver.ResolveHttpsRecordAsync(host!, 443, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception is an ArgumentException", true, exception is not null);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_OnPort443_PostsTheHttpsQueryAndReturnsTheEchConfigList()
    {
        const string RecordData = "000100" + "00010003026832" + "00050006AABBCCDDEEFF";
        var question = "076578616D706C650474657374000041" + "0001";
        var answer = Convert.FromHexString("000081800001000100000000" + question + "C00C004100010000003C0014" + RecordData);
        var connector = Answering(Ok(answer));
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "example.test port 443");
        Diagnostics.Bytes("HTTPS answer", answer);

        var record = await resolver.ResolveHttpsRecordAsync("example.test", 443, CancellationToken.None);

        WriteSent(connector);
        Diagnostics.Act("ALPN", record is null ? "(no record)" : string.Join(",", record.ApplicationProtocols));
        Diagnostics.Act("ECH config list", record is null ? "(no record)" : Convert.ToHexString(record.EchConfigList.Span));
        var head = "POST /dns-query HTTP/1.1\r\n"
            + $"Host: 127.0.0.1:{DohPort}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + "Content-Length: 30\r\n"
            + "\r\n";
        var query = Convert.FromHexString("000001000001000000000000" + question);
        Diagnostics.Assert("connections opened", 1, connector.Opened.Count);
        Assert.HasCount(1, connector.Opened);
        Diagnostics.Diff("HTTPS POST", Encoding.Latin1.GetBytes(head).Concat(query).ToArray(), connector.Opened[0].Written.ToArray());
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(query).ToArray(), connector.Opened[0].Written);
        Assert.IsNotNull(record);
        Diagnostics.Assert("ECH config list", "AABBCCDDEEFF", Convert.ToHexString(record.EchConfigList.Span));
        CollectionAssert.AreEqual(new[] { "h2" }, record.ApplicationProtocols.ToArray());
        Assert.AreEqual("AABBCCDDEEFF", Convert.ToHexString(record.EchConfigList.Span));
    }

    [TestMethod]
    [DataRow("00010003026832" + "00050006AABBCCDDEEFF", "AABBCCDDEEFF")]
    [DataRow("00010003026832", null)]
    public async Task FindEchConfigListAsync_IsTheHttpsRecordsEchParameter(string parameters, string? expected)
    {
        var question = "076578616D706C650474657374000041" + "0001";
        var recordData = "000100" + parameters;
        var answer = Convert.FromHexString("000081800001000100000000" + question + "C00C004100010000003C" + (recordData.Length / 2).ToString("X4") + recordData);
        var resolver = new DohDnsResolver(Answering(Ok(answer)), MeasuredDohUrl);
        Diagnostics.Arrange("record parameters", parameters);
        Diagnostics.Bytes("HTTPS answer", answer);

        var list = await resolver.FindEchConfigListAsync("example.test", 443, CancellationToken.None);

        var listHex = list is null ? null : Convert.ToHexString(list);
        Diagnostics.Act("ECH config list", listHex ?? "null");
        Diagnostics.Assert("ECH config list", expected ?? "null", listHex ?? "null");
        Assert.AreEqual(expected, listHex);
    }

    [TestMethod]
    public async Task FindEchConfigListAsync_WithNoRecord_IsNull()
    {
        ArrangeResolve(MeasuredDohUrl, "example.test port 443, no answer");

        var list = await new DohDnsResolver(new FakeConnector(), MeasuredDohUrl).FindEchConfigListAsync("example.test", 443, CancellationToken.None);

        var listText = list is null ? "null" : Convert.ToHexString(list);
        Diagnostics.Act("ECH config list", listText);
        Diagnostics.Assert("ECH config list", "null", listText);
        Assert.IsNull(list);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_OnAnotherPort_AsksForThePortPrefixedName()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "example.test port 8443");

        var record = await resolver.ResolveHttpsRecordAsync("example.test", 8443, CancellationToken.None);

        WriteSent(connector);
        Diagnostics.Assert("record", "null", record is null ? "null" : "a record");
        Assert.IsNull(record);
        var expected = DnsQueryEncoder.Encode("_8443._https.example.test", DnsRecordType.Https).Bytes;
        var sentQuery = connector.Opened[0].Written.TakeLast(expected.Length).ToArray();
        Diagnostics.Diff("query", expected, sentQuery);
        CollectionAssert.AreEqual(expected, sentQuery);
    }

    // The DoH query bytes curl 8.21.0 (OpenSSL 4.0.0, ECH build) POSTed under --ech true for
    // https://ech.example:9443/ and https://ech.example/, measured with Record-CurlExchange.ps1 -DohPort (BL-1173).
    [TestMethod]
    [DataRow(9443, "000001000001000000000000055F39343433065F687474707303656368076578616D706C650000410001")]
    [DataRow(443, "00000100000100000000000003656368076578616D706C650000410001")]
    public async Task ResolveHttpsRecordAsync_WritesTheMeasuredQueryBytes(int port, string expectedHex)
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, $"ech.example port {port}");

        _ = await resolver.ResolveHttpsRecordAsync("ech.example", port, CancellationToken.None);

        WriteSent(connector);
        var expected = Convert.FromHexString(expectedHex);
        var sentQuery = connector.Opened[0].Written.TakeLast(expected.Length).ToArray();
        Diagnostics.Diff("query", expected, sentQuery);
        CollectionAssert.AreEqual(expected, sentQuery);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("[::1]")]
    [DataRow("localhost")]
    public async Task ResolveHttpsRecordAsync_ForALiteralOrLocalhost_AsksNothing(string host)
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, host);

        var record = await resolver.ResolveHttpsRecordAsync(host, 443, CancellationToken.None);

        Diagnostics.Act("record", record is null ? "null" : "a record");
        Diagnostics.Assert("DoH connections asked for", 0, connector.Targets.Count);
        Assert.IsNull(record);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_ForANameWithAnEmptyLabel_AsksNothing()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "a..test");

        var record = await resolver.ResolveHttpsRecordAsync("a..test", 443, CancellationToken.None);

        Diagnostics.Act("record", record is null ? "null" : "a record");
        Diagnostics.Assert("DoH connections asked for", 0, connector.Targets.Count);
        Assert.IsNull(record);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_WhenTheAnswerHoldsNoHttpsRecord_ReturnsNull()
    {
        var connector = Answering(Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback)));
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);
        ArrangeResolve(MeasuredDohUrl, "example.test port 443, answered with an A record");

        var record = await resolver.ResolveHttpsRecordAsync("example.test", 443, CancellationToken.None);

        Diagnostics.Act("record", record is null ? "null" : "a record");
        Diagnostics.Assert("record", "null", record is null ? "null" : "a record");
        Assert.IsNull(record);
    }

    [TestMethod]
    public void HttpsQueryName_FollowsRfc9460Section9_1()
    {
        Diagnostics.Arrange("names", "example.test on 443 and on 80");

        var onDefaultPort = DohDnsResolver.HttpsQueryName("example.test", 443);
        var onPort80 = DohDnsResolver.HttpsQueryName("example.test", 80);

        Diagnostics.Act("query names", $"{onDefaultPort}, {onPort80}");
        Diagnostics.Assert("query name on 443", "example.test", onDefaultPort);
        Diagnostics.Assert("query name on 80", "_80._https.example.test", onPort80);
        Assert.AreEqual("example.test", onDefaultPort);
        Assert.AreEqual("_80._https.example.test", onPort80);
    }

    private async Task AssertCouldNotResolveAsync(IConnector dohConnector)
    {
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(new DohDnsResolver(dohConnector, MeasuredDohUrl), dialer, new FakeTlsProvider(), new ManualTimeProvider());
        ArrangeResolve(MeasuredDohUrl, "example.test port 80, through TcpConnector");

        var result = await connector.ConnectAsync(new ConnectTarget("example.test", 80, UseTls: false), CancellationToken.None);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("dialed", dialer.DialedEndPoints.Count);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Diagnostics.Diff("error message", "Could not resolve host: example.test", result.ErrorMessage ?? "(null)");
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.test", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    private void ArrangeResolve(Uri dohUrl, string host)
    {
        Diagnostics.Arrange("DoH URL", dohUrl);
        Diagnostics.Arrange("host", host);
    }

    private void WriteSent(FakeConnector connector)
    {
        Diagnostics.Act("connections opened", connector.Opened.Count);
        for (var index = 0; index < connector.Opened.Count; index++)
        {
            Diagnostics.Bytes($"POST {index + 1}", connector.Opened[index].Written.ToArray());
        }
    }

    private void WriteTargets(FakeConnector connector) =>
        Diagnostics.Act("targets", string.Join(", ", connector.Targets));

    private void WriteAddresses(IPAddress[] expected, IEnumerable<IPAddress> addresses)
    {
        var actual = string.Join(", ", addresses);
        Diagnostics.Act("addresses", actual.Length == 0 ? "(none)" : actual);
        Diagnostics.Assert("addresses", string.Join(", ", (object[])expected), actual);
    }

    private static FakeConnector Answering(params byte[][] responses)
    {
        var connector = new FakeConnector();
        connector.BytesToRead.AddRange(responses);
        return connector;
    }

    private static byte[] QueryFor(DnsRecordType recordType) => DnsQueryEncoder.Encode("example.test", recordType).Bytes;

    private static byte[] AnswerTo(DnsRecordType recordType, params IPAddress[] addresses) =>
        DnsTestReplies.Answer(QueryFor(recordType), addresses);

    private static byte[] Ok(byte[] answer) =>
        [.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];

    private static byte[] Response(string text) => Encoding.Latin1.GetBytes(text);
}
