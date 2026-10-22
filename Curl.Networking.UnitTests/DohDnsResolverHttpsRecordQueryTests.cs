using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the HTTPS (type 65) query curl 8.22.0's HTTPSRR build sends through <c>--doh-url</c> for an
/// <c>https://</c> origin before its A and AAAA queries (ADR-0727, BL-2920; upstream case 2100): its
/// bytes, its place first, the record kept for <c>--ech</c>, and which connects
/// <see cref="TcpConnector" /> asks it for.
/// </summary>
[TestClass]
public sealed class DohDnsResolverHttpsRecordQueryTests
{
    private const int OriginPort = 41443;

    private const string Header = "000001000001000000000000";

    private const string FooExampleCom = "03666F6F076578616D706C6503636F6D00";

    private static readonly Uri DohUrl = new("https://127.0.0.1:41444/dns-query");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Upstream case 2100's protocol under %if HTTPSRR: the HTTPS query for
    // _<port>._https.foo.example.com (Content-Length 47 for a five-digit port), then A and AAAA.
    [TestMethod]
    public async Task ResolveHttpsOriginAsync_OnANonDefaultPort_PostsTheHttpsQueryBeforeTheAAndAaaaQueries()
    {
        var httpsQuery = Convert.FromHexString(Header + "065F3431343433065F6874747073" + FooExampleCom + "00410001");
        var queryA = Convert.FromHexString(Header + FooExampleCom + "00010001");
        var queryAaaa = Convert.FromHexString(Header + FooExampleCom + "001C0001");
        var connector = Answering(
            Ok(DnsTestReplies.Answer(httpsQuery, [])),
            Ok(DnsTestReplies.Answer(queryA, [IPAddress.Loopback])),
            Ok(DnsTestReplies.Answer(queryAaaa, [IPAddress.IPv6Loopback])));
        var resolver = new DohDnsResolver(connector, DohUrl);
        Diagnostics.Arrange("origin", $"https://foo.example.com:{OriginPort}/ through {DohUrl}");

        var addresses = await resolver.ResolveHttpsOriginAsync("foo.example.com", OriginPort, CancellationToken.None);

        WriteSent(connector);
        Diagnostics.Assert("connections opened", 3, connector.Opened.Count);
        Assert.HasCount(3, connector.Opened);
        AssertPost(httpsQuery, connector.Opened[0].Written.ToArray(), "HTTPS POST");
        AssertPost(queryA, connector.Opened[1].Written.ToArray(), "A POST");
        AssertPost(queryAaaa, connector.Opened[2].Written.ToArray(), "AAAA POST");
        Diagnostics.Assert("addresses", "::1, 127.0.0.1", string.Join(", ", addresses));
        CollectionAssert.AreEqual(new[] { IPAddress.IPv6Loopback, IPAddress.Loopback }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveHttpsRecordAsync_AfterResolveHttpsOriginAsync_ReturnsTheKeptRecordAndAsksNothingMore()
    {
        var question = FooExampleCom + "00410001";
        var httpsAnswer = Convert.FromHexString("000081800001000100000000" + question + "C00C004100010000003C0014" + "000100" + "00010003026832" + "00050006AABBCCDDEEFF");
        var queryA = Convert.FromHexString(Header + FooExampleCom + "00010001");
        var queryAaaa = Convert.FromHexString(Header + FooExampleCom + "001C0001");
        var connector = Answering(Ok(httpsAnswer), Ok(DnsTestReplies.Answer(queryA, [IPAddress.Loopback])), Ok(DnsTestReplies.Answer(queryAaaa, [])));
        var resolver = new DohDnsResolver(connector, DohUrl);
        Diagnostics.Arrange("origin", "https://foo.example.com/ with an HTTPS record holding ech AABBCCDDEEFF");

        _ = await resolver.ResolveHttpsOriginAsync("foo.example.com", 443, CancellationToken.None);
        var record = await resolver.ResolveHttpsRecordAsync("foo.example.com", 443, CancellationToken.None);

        WriteSent(connector);
        Diagnostics.Act("ECH config list", record is null ? "(no record)" : Convert.ToHexString(record.EchConfigList.Span));
        Diagnostics.Assert("connections opened", 3, connector.Opened.Count);
        Assert.HasCount(3, connector.Opened);
        Assert.IsNotNull(record);
        Assert.AreEqual("AABBCCDDEEFF", Convert.ToHexString(record.EchConfigList.Span));
    }

    [TestMethod]
    public async Task HttpsRecordFor_AfterResolveHttpsOriginAsync_GivesTheKeptRecordForThatPortOnly()
    {
        var question = FooExampleCom + "00410001";
        var httpsAnswer = Convert.FromHexString("000081800001000100000000" + question + "C00C004100010000003C0014" + "000100" + "00010003026832" + "00050006AABBCCDDEEFF");
        var queryA = Convert.FromHexString(Header + FooExampleCom + "00010001");
        var queryAaaa = Convert.FromHexString(Header + FooExampleCom + "001C0001");
        var connector = Answering(Ok(httpsAnswer), Ok(DnsTestReplies.Answer(queryA, [IPAddress.Loopback])), Ok(DnsTestReplies.Answer(queryAaaa, [])));
        var resolver = new DohDnsResolver(connector, DohUrl);
        Diagnostics.Arrange("origin", "https://foo.example.com/ with an HTTPS record 1 . alpn=h2 ech");

        _ = await resolver.ResolveHttpsOriginAsync("foo.example.com", 443, CancellationToken.None);
        var kept = resolver.HttpsRecordFor("foo.example.com", 443);
        var otherPort = resolver.HttpsRecordFor("foo.example.com", 8443);

        Diagnostics.Act("kept record", kept is null ? "(none)" : DohTraceLines.FormatHttpsRecord(kept, hasMandatoryKeys: false));
        Diagnostics.Assert("other port", "(none)", otherPort is null ? "(none)" : "a record");
        Assert.IsNotNull(kept);
        Assert.AreEqual("1 . alpn=h2 ech=<6 bytes>", DohTraceLines.FormatHttpsRecord(kept, hasMandatoryKeys: false));
        Assert.IsNull(otherPort);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task ResolveHttpsOriginAsync_WithNoHost_Throws(string? host)
    {
        Diagnostics.Arrange("host", host is null ? "null" : $"\"{host}\"");
        var resolver = new DohDnsResolver(new FakeConnector(), DohUrl);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await resolver.ResolveHttpsOriginAsync(host!, 443, CancellationToken.None));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception is an ArgumentException", true, exception is not null);
    }

    [TestMethod]
    public async Task ConnectAsync_ToADirectHttpsOrigin_AsksTheResolverForTheHttpsOriginWithItsPort()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        Diagnostics.Arrange("target", $"https://foo.example.com:{OriginPort}/");

        _ = await connector.ConnectAsync(new ConnectTarget("foo.example.com", OriginPort, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);

        Diagnostics.Assert("look-ups", $"https-origin foo.example.com:{OriginPort}", string.Join(" | ", resolver.Calls));
        CollectionAssert.AreEqual(new[] { $"https-origin foo.example.com:{OriginPort}" }, resolver.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpOrigin_AsksOnlyForTheAddresses()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        Diagnostics.Arrange("target", "http://foo.example.com:41443/");

        _ = await connector.ConnectAsync(new ConnectTarget("foo.example.com", OriginPort, UseTls: false) { PoolScheme = "http" }, CancellationToken.None);

        Diagnostics.Assert("look-ups", "addresses foo.example.com", string.Join(" | ", resolver.Calls));
        CollectionAssert.AreEqual(new[] { "addresses foo.example.com" }, resolver.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginThroughAProxy_AsksOnlyForTheProxysAddresses()
    {
        var resolver = new RecordingHttpsOriginResolver();
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        var target = new ConnectTarget("foo.example.com", OriginPort, UseTls: true) { PoolScheme = "https", Proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null) };
        Diagnostics.Arrange("target", "https://foo.example.com:41443/ through socks5h://proxy.example:1080");

        _ = await connector.ConnectAsync(target, CancellationToken.None);

        Diagnostics.Assert("first look-up", "addresses proxy.example", resolver.Calls.FirstOrDefault());
        Assert.AreEqual("addresses proxy.example", resolver.Calls.FirstOrDefault());
        Assert.IsFalse(resolver.Calls.Any(call => call.StartsWith("https-origin", StringComparison.Ordinal)));
    }

    private void AssertPost(byte[] query, byte[] written, string label)
    {
        var head = "POST /dns-query HTTP/1.1\r\n"
            + "Host: 127.0.0.1:41444\r\n"
            + "Accept: */*\r\n"
            + "Content-Type: application/dns-message\r\n"
            + $"Content-Length: {query.Length}\r\n"
            + "\r\n";
        byte[] expected = [.. Encoding.Latin1.GetBytes(head), .. query];
        Diagnostics.Diff(label, expected, written);
        CollectionAssert.AreEqual(expected, written);
    }

    private void WriteSent(FakeConnector connector)
    {
        Diagnostics.Act("connections opened", connector.Opened.Count);
        for (var index = 0; index < connector.Opened.Count; index++)
        {
            Diagnostics.Bytes($"POST {index + 1}", connector.Opened[index].Written.ToArray());
        }
    }

    private static FakeConnector Answering(params byte[][] responses)
    {
        var connector = new FakeConnector();
        connector.BytesToRead.AddRange(responses);
        return connector;
    }

    private static byte[] Ok(byte[] answer) =>
        [.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];

    // Records which of its two look-ups each resolve asked for, answering 127.0.0.1.
    private sealed class RecordingHttpsOriginResolver : IHttpsOriginDnsResolver
    {
        public List<string> Calls { get; } = [];

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Calls.Add($"addresses {host}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveHttpsOriginAsync(string host, int port, CancellationToken cancellationToken)
        {
            Calls.Add($"https-origin {host}:{port}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }

        public ServiceBindingRecord? HttpsRecordFor(string host, int port) => null;
    }
}
