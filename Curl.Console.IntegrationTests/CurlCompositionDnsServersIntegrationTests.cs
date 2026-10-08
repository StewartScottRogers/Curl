using System.Net;
using System.Net.Sockets;

using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins, over a real loopback DNS server and TCP listener, that <c>--dns-servers</c> makes
/// every transfer connect to the address that server answered (ADR-0170, BL-694). The
/// socket-free cases stay in <c>CurlCompositionDnsServersTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionDnsServersIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CreateTransports_WithDnsServersOnLoopback_ConnectsToTheAddressTheServerAnswered()
    {
        using UdpClient dnsServer = new(new IPEndPoint(IPAddress.Loopback, 0));
        using TcpListener webServer = new(IPAddress.Loopback, 0);
        webServer.Start();
        int dnsPort = ((IPEndPoint)dnsServer.Client.LocalEndPoint!).Port;
        int webPort = ((IPEndPoint)webServer.LocalEndpoint).Port;
        Task answering = AnswerOneQueryAsync(dnsServer);
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-4", "--dns-servers", $"127.0.0.1:{dnsPort}"));

        ConnectResult result;
        using (Diagnostics.Phase("connect"))
        {
            result = await transports.TcpConnector.ConnectAsync(new ConnectTarget("bl694.example", webPort, false), CancellationToken.None);
        }

        await answering;
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("connected", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection, result.ErrorMessage);
        await result.Connection.DisposeAsync();
    }

    /// <summary>Answers one A query with 127.0.0.1: the query's header and question, flags 0x8180, one record.</summary>
    private static async Task AnswerOneQueryAsync(UdpClient dnsServer)
    {
        UdpReceiveResult received = await dnsServer.ReceiveAsync();
        byte[] query = received.Buffer;
        int questionEnd = 12;
        while (query[questionEnd] != 0)
        {
            questionEnd += 1 + query[questionEnd];
        }

        questionEnd += 5;
        byte[] reply = [.. query.AsSpan(0, questionEnd), 0xC0, 0x0C, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 127, 0, 0, 1];
        reply[2] = 0x81;
        reply[3] = 0x80;
        reply[7] = 1;
        reply[10] = 0;
        reply[11] = 0;
        await dnsServer.SendAsync(reply, received.RemoteEndPoint);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://bl694.example/")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://bl694.example/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
