using System.Text;

using Curl.Api.Options.Tls;
using Curl.Cli;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins that under the conformance harness's <see cref="EngineDialing" />, <c>--doh-url</c> still
/// resolves through a <see cref="DohDnsResolver" />, whose DoH connections are dialed through the
/// harness's dialer (BL-2919); without <c>--doh-url</c> the harness's resolver is the run's. The DoH
/// server is a <see cref="ScriptedConnector" />, so no socket is opened.
/// </summary>
[TestClass]
public sealed class EngineCompositionDohDialingTests
{
    private const string DohUrl = "http://127.0.0.1:48711/dns-query";

    // ID 0, flags 0x8180, one question for example.test A IN, one answer A IN TTL 60 127.0.0.1.
    private static readonly byte[] AAnswer =
    [
        .. Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: 46\r\n\r\n"),
        0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x07, .. "example"u8, 0x04, .. "test"u8, 0x00, 0x00, 0x01, 0x00, 0x01,
        0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01,
    ];

    [TestMethod]
    public async Task CreateTransports_DialingAndDohUrl_SendsTheDohPostsThroughTheDialersConnection()
    {
        ScriptedConnector dohServer = new([AAnswer, AAnswer]);
        EngineDialing dialing = new(new ScriptedTcpDialer(dohServer), new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp"));

        CurlTransports transports = CreateTransports(dialing, "--doh-url", DohUrl);
        IReadOnlyList<System.Net.IPAddress> addresses = await transports.DnsResolver.ResolveAsync("example.test", CancellationToken.None);

        Assert.IsInstanceOfType<DohDnsResolver>(transports.DnsResolver);
        Assert.AreEqual("127.0.0.1", addresses.Single().ToString());
        Assert.IsNotEmpty(dohServer.Targets);
        Assert.AreEqual(48711, dohServer.Targets[0].Port);
        StringAssert.Contains(Encoding.Latin1.GetString(dohServer.Written), "POST /dns-query HTTP/1.1");
    }

    [TestMethod]
    public void CreateTransports_DialingWithoutDohUrl_KeepsTheDialersResolver()
    {
        LoopbackDnsResolver harnessResolver = new();
        EngineDialing dialing = new(new ScriptedTcpDialer(new ScriptedConnector([])), harnessResolver, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no udp"));

        CurlTransports transports = CreateTransports(dialing);

        Assert.AreSame(harnessResolver, transports.DnsResolver);
    }

    private static CurlTransports CreateTransports(EngineDialing dialing, params string[] arguments)
    {
        CommandLineParseResult parsed = OpenSslBuildParser.Parse([.. arguments, "http://example.test:48712/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        CommandLineOptions options = parsed.Options;
        return EngineComposition.CreateTransports(
            EasyOptionsFromCommandLine.ToolOptionsOf(options),
            EasyOptionsFromCommandLine.NetworkOf(options),
            EasyOptionsFromCommandLine.TlsOf(options),
            EasyOptionsFromCommandLine.HttpOf(options),
            EasyOptionsFromCommandLine.ProxyOf(options),
            EasyOptionsFromCommandLine.AuthenticationOf(options),
            TimeProvider.System,
            null,
            null,
            null,
            null,
            null,
            null,
            resolver => resolver,
            dialing);
    }
}
