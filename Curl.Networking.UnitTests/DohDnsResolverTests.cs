using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Constructor_WithNullConnector_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new DohDnsResolver(null!, MeasuredDohUrl));

        Assert.AreEqual("connector", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithNullUrl_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new DohDnsResolver(new FakeConnector(), null!));

        Assert.AreEqual("dohUrl", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_WithRelativeUrl_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new DohDnsResolver(new FakeConnector(), new Uri("/dns-query", UriKind.Relative)));

        Assert.AreEqual("dohUrl", exception.ParamName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ResolveAsync_WithNoHost_Throws(string? host)
    {
        var resolver = new DohDnsResolver(new FakeConnector(), MeasuredDohUrl);

        await Assert.ThrowsAsync<ArgumentException>(async () => await resolver.ResolveAsync(host!, CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_ThePostsCurlSent_AreWrittenByteForByte()
    {
        // ADR-0152: curl 8.21.0 POSTed this for example.test's A query, and the same with
        // QTYPE 00 1C for AAAA, each on a connection of its own, A first.
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        var head = "POST /dns-query HTTP/1.1\r\n"
            + $"Host: 127.0.0.1:{DohPort}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + "Content-Length: 30\r\n"
            + "\r\n";
        var queryA = Convert.FromHexString("000001000001000000000000076578616D706C650474657374000001" + "0001");
        var queryAaaa = Convert.FromHexString("000001000001000000000000076578616D706C65047465737400001C" + "0001");
        Assert.HasCount(2, connector.Opened);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(queryA).ToArray(), connector.Opened[0].Written);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(queryAaaa).ToArray(), connector.Opened[1].Written);
        Assert.IsTrue(connector.Opened.All(connection => connection.FlushCount == 1 && connection.IsDisposed));
    }

    [TestMethod]
    public async Task ResolveAsync_ConnectsToTheDohServerOverTlsOfferingHttp11()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        var target = new ConnectTarget("127.0.0.1", DohPort, UseTls: true) { PoolScheme = "https" };
        CollectionAssert.AreEqual(new[] { target, target }, connector.Targets);
        CollectionAssert.AreEqual(new[] { "http/1.1" }, TcpConnector.ApplicationProtocolsFor(connector.Targets[0], ["http/1.1"]).ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_WithTheSchemesDefaultPort_WritesHostWithoutAPortAndKeepsTheQuery()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, new Uri("https://dns.example/dns-query?ct=1"));

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        var written = Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray());
        StringAssert.StartsWith(written, "POST /dns-query?ct=1 HTTP/1.1\r\nHost: dns.example\r\nAccept: */*\r\n");
        Assert.AreEqual(new ConnectTarget("dns.example", 443, UseTls: true) { PoolScheme = "https" }, connector.Targets[0]);
    }

    [TestMethod]
    public async Task ResolveAsync_WithAnIPv6DohServer_BracketsItInHostAndConnectsWithout()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, new Uri("https://[::1]:8443/q"));

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        var written = Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray());
        StringAssert.Contains(written, "\r\nHost: [::1]:8443\r\n");
        Assert.AreEqual("::1", connector.Targets[0].Host);
    }

    [TestMethod]
    public async Task ResolveAsync_WithAnHttpDohServer_ConnectsWithoutTls()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, new Uri("http://dns.example/dns-query"));

        await resolver.ResolveAsync("example.test", CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("dns.example", 80, UseTls: false) { PoolScheme = "http" }, connector.Targets[0]);
        StringAssert.Contains(Encoding.Latin1.GetString(connector.Opened[0].Written.ToArray()), "\r\nHost: dns.example\r\n");
    }

    [TestMethod]
    public async Task ResolveAsync_TheMeasuredAAnswerAndAnEmptyAaaaAnswer_ReturnsTheIPv4Address()
    {
        // ADR-0152: A answered 127.0.0.1 TTL 60, AAAA NOERROR with no record; curl printed
        // "IPv6: (none)" and "IPv4: 127.0.0.1".
        var connector = Answering(Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback)), Ok(AnswerTo(DnsRecordType.Aaaa, IPAddress.Loopback)));

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AAndAaaaAnswers_ReturnsTheIPv6AddressesFirst()
    {
        var connector = Answering(
            Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback, IPAddress.Parse("192.0.2.1"))),
            Ok(AnswerTo(DnsRecordType.Aaaa, IPv6Address)));

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

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

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AnIPAddressLiteral_IsReturnedWithoutAQuery()
    {
        // curl -v --doh-url ... http://127.0.0.2:P/ and http://[::1]:P/ asked no DoH server (measured).
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        var ipv4 = await resolver.ResolveAsync("127.0.0.2", CancellationToken.None);
        var ipv6 = await resolver.ResolveAsync("[::1]", CancellationToken.None);

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

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync(host, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_AHostTheEncoderRefuses_SendsNoQuery()
    {
        var connector = new FakeConnector();

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("a..test", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheDohServerCannotBeReached_ReturnsNoAddress()
    {
        // curl gave exit 6 after about 2 s with nothing listening on the DoH port (ADR-0152).
        var connector = new FakeConnector { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect") };

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.HasCount(2, connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_WhenTheConnectionFailsMidExchange_ReturnsNoAddressAndDisposesIt()
    {
        var connector = new FakeConnector { ReadException = new IOException("reset") };

        var addresses = await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsTrue(connector.Opened.All(connection => connection.IsDisposed));
    }

    [TestMethod]
    public async Task ResolveAsync_WhenCancelled_Throws()
    {
        var connector = new FakeConnector { ReadException = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new DohDnsResolver(connector, MeasuredDohUrl).ResolveAsync("example.test", CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenBothDohAnswersAre500WithNoBody_FailsWithExit6()
    {
        // ADR-0152: "500, empty body, both connections" -> curl: (6) Could not resolve host: example.test.
        var connector = Answering(Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"), Response("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n"));

        await AssertCouldNotResolveAsync(connector);
    }

    [TestMethod]
    public async Task ResolveAsync_ThroughTcpConnector_WhenBothDohAnswersAreNxdomain_FailsWithExit6()
    {
        // ADR-0152: RCODE 3 in both answers -> "DoH: Bad RCODE type A ...", exit 6.
        var connector = Answering(
            Ok(DnsTestReplies.Answer(QueryFor(DnsRecordType.A), [IPAddress.Loopback], responseCode: 3)),
            Ok(DnsTestReplies.Answer(QueryFor(DnsRecordType.Aaaa), [IPv6Address], responseCode: 3)));

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

        await AssertCouldNotResolveAsync(dohConnector);

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

        var result = await connector.ConnectAsync(new ConnectTarget("example.test", 48637, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.Loopback, 48637) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ResolveHttpsRecordAsync_WithNoHost_Throws(string? host)
    {
        var resolver = new DohDnsResolver(new FakeConnector(), MeasuredDohUrl);

        await Assert.ThrowsAsync<ArgumentException>(async () => await resolver.ResolveHttpsRecordAsync(host!, 443, CancellationToken.None));
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_OnPort443_PostsTheHttpsQueryAndReturnsTheEchConfigList()
    {
        const string RecordData = "000100" + "00010003026832" + "00050006AABBCCDDEEFF";
        var question = "076578616D706C650474657374000041" + "0001";
        var answer = Convert.FromHexString("000081800001000100000000" + question + "C00C004100010000003C0014" + RecordData);
        var connector = Answering(Ok(answer));
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        var record = await resolver.ResolveHttpsRecordAsync("example.test", 443, CancellationToken.None);

        var head = "POST /dns-query HTTP/1.1\r\n"
            + $"Host: 127.0.0.1:{DohPort}\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + "Content-Length: 30\r\n"
            + "\r\n";
        var query = Convert.FromHexString("000001000001000000000000" + question);
        Assert.HasCount(1, connector.Opened);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(head).Concat(query).ToArray(), connector.Opened[0].Written);
        Assert.IsNotNull(record);
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

        var list = await resolver.FindEchConfigListAsync("example.test", 443, CancellationToken.None);

        Assert.AreEqual(expected, list is null ? null : Convert.ToHexString(list));
    }

    [TestMethod]
    public async Task FindEchConfigListAsync_WithNoRecord_IsNull() =>
        Assert.IsNull(await new DohDnsResolver(new FakeConnector(), MeasuredDohUrl).FindEchConfigListAsync("example.test", 443, CancellationToken.None));

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_OnAnotherPort_AsksForThePortPrefixedName()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        var record = await resolver.ResolveHttpsRecordAsync("example.test", 8443, CancellationToken.None);

        Assert.IsNull(record);
        var expected = DnsQueryEncoder.Encode("_8443._https.example.test", DnsRecordType.Https).Bytes;
        CollectionAssert.AreEqual(expected, connector.Opened[0].Written.TakeLast(expected.Length).ToArray());
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("[::1]")]
    [DataRow("localhost")]
    public async Task ResolveHttpsRecordAsync_ForALiteralOrLocalhost_AsksNothing(string host)
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        Assert.IsNull(await resolver.ResolveHttpsRecordAsync(host, 443, CancellationToken.None));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_ForANameWithAnEmptyLabel_AsksNothing()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        Assert.IsNull(await resolver.ResolveHttpsRecordAsync("a..test", 443, CancellationToken.None));
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_WhenTheAnswerHoldsNoHttpsRecord_ReturnsNull()
    {
        var connector = Answering(Ok(AnswerTo(DnsRecordType.A, IPAddress.Loopback)));
        var resolver = new DohDnsResolver(connector, MeasuredDohUrl);

        Assert.IsNull(await resolver.ResolveHttpsRecordAsync("example.test", 443, CancellationToken.None));
    }

    [TestMethod]
    public void HttpsQueryName_FollowsRfc9460Section9_1()
    {
        Assert.AreEqual("example.test", DohDnsResolver.HttpsQueryName("example.test", 443));
        Assert.AreEqual("_80._https.example.test", DohDnsResolver.HttpsQueryName("example.test", 80));
    }

    private static async Task AssertCouldNotResolveAsync(IConnector dohConnector)
    {
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(new DohDnsResolver(dohConnector, MeasuredDohUrl), dialer, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget("example.test", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: example.test", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
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
