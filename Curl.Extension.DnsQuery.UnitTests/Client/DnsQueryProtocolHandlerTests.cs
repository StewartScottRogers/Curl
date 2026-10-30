using System.Net;
using System.Text;
using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.UnitTests.Transport;
using Curl.Extension.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Client;

/// <summary>Checks a <c>dns://</c> transfer end to end over a fake server (RFC 1035 sections 4.1 and 4.2, RFC 4501 section 2).</summary>
[TestClass]
public sealed class DnsQueryProtocolHandlerTests
{
    private static readonly byte[] Answer = DnsWire.SingleAnswer(1, [192, 0, 2, 1]);

    [TestMethod]
    public void SupportedSchemes_IsDns() =>
        CollectionAssert.AreEqual(new[] { "dns" }, HandlerOver(new ScriptedDnsServer((_, _) => [], [])).SupportedSchemes.ToArray());

    [TestMethod]
    public async Task ExecuteAsync_ByDefault_QueriesOverUdpAndWritesText()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        var output = new MemoryStream();

        TransferResult result = await HandlerOver(server).ExecuteAsync(ContextOf("dns://192.0.2.53/example.com?type=A", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string text = Encoding.UTF8.GetString(output.ToArray());
        Assert.StartsWith(";; ->>HEADER<<- opcode: QUERY, status: NOERROR, id: 4660\n", text);
        Assert.Contains("192.0.2.1", text);
        Assert.AreEqual(("udp", "192.0.2.53", 53), server.Opened.Single());
        byte[] query = server.Datagrams.Single();
        CollectionAssert.AreEqual(new byte[] { 0x12, 0x34, 0x01, 0x00 }, query[..4], "RFC 1035 section 4.1.1: the ID, and RD set");
    }

    [TestMethod]
    public async Task ExecuteAsync_DnsJson_WritesOneLineOfJson()
    {
        var output = new MemoryStream();

        TransferResult result = await HandlerOver(new ScriptedDnsServer((_, _) => [Answer], []))
            .ExecuteAsync(new DnsQueryContext(ContextOf("dns://h/example.com", output), "udp", json: true));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string json = Encoding.UTF8.GetString(output.ToArray());
        Assert.StartsWith("{\"id\":4660,", json);
        Assert.EndsWith("}\n", json);
    }

    [TestMethod]
    public async Task ExecuteAsync_DnsTransportTcp_QueriesOverTcp()
    {
        var server = new ScriptedDnsServer((_, _) => [], [.. DnsWire.UInt16(Answer.Length), .. Answer]);

        TransferResult result = await HandlerOver(server).ExecuteAsync(new DnsQueryContext(ContextOf("dns://h:5353/example.com", new MemoryStream()), "tcp", json: false));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("tcp", "h", 5353), server.Opened.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoServerInTheUrl_QueriesTheSystemsFirstDnsServer()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        Assert.IsTrue(CurlUrl.TryParseAllowingEmptyHost("dns:///example.com", false, false, out CurlUrl? url, out _));
        var handler = new DnsQueryProtocolHandler(new(server, server, null!, null!), () => [new IPEndPoint(IPAddress.Parse("198.51.100.1"), 53), new IPEndPoint(IPAddress.Parse("198.51.100.2"), 53)], _ => [0x12, 0x34]);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = url, Output = new MemoryStream(), TimeProvider = new ImmediateTimeProvider() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("udp", "198.51.100.1", 53), server.Opened.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoServerInTheUrlNorTheSystem_FailsWithExit6()
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);
        Assert.IsTrue(CurlUrl.TryParseAllowingEmptyHost("dns:///example.com", false, false, out CurlUrl? url, out _));
        var handler = new DnsQueryProtocolHandler(new(server, server, null!, null!), () => [], _ => [0x12, 0x34]);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = url, Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.IsEmpty(server.Opened);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnswerWithAnotherId_FailsWithExit8()
    {
        byte[] otherId = [.. DnsWire.UInt16(Answer.Length), .. Answer];
        otherId[3] ^= 0xFF;
        var server = new ScriptedDnsServer((_, _) => [], otherId);

        TransferResult result = await HandlerOver(server).ExecuteAsync(new DnsQueryContext(ContextOf("dns://h/example.com", new MemoryStream()), "tcp", json: false));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedAnswer_FailsWithExit8()
    {
        var server = new ScriptedDnsServer((_, _) => [], [0, 12, 0x12, 0x34, 0x81, 0x80, 0, 1, 0, 0, 0, 0, 0, 0]);

        TransferResult result = await HandlerOver(server).ExecuteAsync(new DnsQueryContext(ContextOf("dns://h/example.com", new MemoryStream()), "tcp", json: false));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    [DataRow("dns://h/example.com?type=NOPE")]
    [DataRow("dns://h/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.example")]
    public async Task ExecuteAsync_UrlThatMakesNoQuery_FailsWithExit3WithoutSending(string url)
    {
        var server = new ScriptedDnsServer((_, _) => [Answer], []);

        TransferResult result = await HandlerOver(server).ExecuteAsync(ContextOf(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(server.Opened);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await HandlerOver(new ScriptedDnsServer((_, _) => [], [])).ExecuteAsync(null!));

    private static DnsQueryProtocolHandler HandlerOver(ScriptedDnsServer server) =>
        new(new ProtocolHandlerServices(server, server, null!, null!), () => [], _ => [0x12, 0x34]);

    private static TransferContext ContextOf(string url, Stream output) =>
        new() { Url = CurlUrl.Parse(url), Output = output, TimeProvider = new ImmediateTimeProvider() };
}
