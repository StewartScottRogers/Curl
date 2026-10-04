using System.Text;

using Curl.Authentication;
using Curl.Cli;
using Curl.Core;
using Curl.Http2;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--proxy-http2</c> from the parsed command line to the connector (ADR-0408, BL-1415):
/// the tunnel options carry it, and a transfer through an <c>https://</c> proxy that agrees
/// <c>h2</c> runs inside an HTTP/2 <c>CONNECT</c> stream. The command line is parsed as the
/// OpenSSL build parses it, since the Schannel build refuses the option, so the tests hold on
/// every platform.
/// </summary>
public sealed partial class CurlCompositionProxyTests
{
    [TestMethod]
    public void CreateProxyTunnelOptions_ProxyHttp2_AsksTheConnectorForAnHttp2Tunnel()
    {
        HttpProxyTunnelOptions options = OpenSslBuildTunnelOptionsFor(["--proxy-http2", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a"]);

        Assert.IsTrue(options.ProxyHttp2);
    }

    [TestMethod]
    [DataRow(new[] { "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a" }, DisplayName = "left out")]
    [DataRow(new[] { "--proxy-http2", "--no-proxy-http2", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a" }, DisplayName = "--no-proxy-http2")]
    public void CreateProxyTunnelOptions_WithoutProxyHttp2_LeavesTheHttp2TunnelOff(string[] arguments)
    {
        HttpProxyTunnelOptions options = OpenSslBuildTunnelOptionsFor(arguments);

        Assert.IsFalse(options.ProxyHttp2);
    }

    [TestMethod]
    public async Task RunAsync_ProxyHttp2ThroughAnH2HttpsProxy_RunsTheTransferInsideAnHttp2ConnectStream()
    {
        HttpProxyTunnelOptions tunnelOptions = OpenSslBuildTunnelOptionsFor(["--proxy-http2", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a"]);
        ScriptedConnector server = new([
            [
                .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
                .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new HeaderField(":status", "200")]), isEndStream: false, isEndHeaders: true)),
                .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, Latin1(Hello), isEndStream: false)),
            ],
        ]);
        TcpConnector connector = new(new LoopbackDnsResolver(), new ScriptedTcpDialer(server), new H2AgreeingTlsProvider(), TimeProvider.System, tunnelOptions);

        Run run = await RunAsync(connector, new Dictionary<string, string>(), ["-sS", "-p", "-x", "https://127.0.0.1:18443", "http://example.com/a"]);

        Assert.AreEqual(0, run.ExitCode, run.StandardError);
        Assert.AreEqual("hello", run.StandardOutput);
        byte[] written = server.Written;
        CollectionAssert.AreEqual(Http2Connection.ClientPreface.ToArray(), written.Take(Http2Connection.ClientPreface.Length).ToArray());
        List<Http2Frame> frames = await ReadFramesAsync(written[Http2Connection.ClientPreface.Length..]);
        Http2Frame connect = frames.First(frame => frame.Type == Http2FrameType.Headers);
        CollectionAssert.AreEqual(
            new[] { ":method: CONNECT", ":authority: example.com:80", "user-agent: curl/8.21.0" },
            new HpackDecoder().Decode(Http2FramePayloadParser.ParseHeaders(connect).Fragment.Span).Select(field => $"{field.Name}: {field.Value}").ToArray());
        Http2Frame tunnelled = frames.First(frame => frame.Type == Http2FrameType.Data);
        Assert.AreEqual(1, tunnelled.StreamId);
        Assert.AreEqual(TunnelledGet, Encoding.Latin1.GetString(tunnelled.Payload.Span));
    }

    /// <summary>The tunnel options the production composition maps from <paramref name="arguments" /> parsed as curl's OpenSSL build parses them.</summary>
    private static HttpProxyTunnelOptions OpenSslBuildTunnelOptionsFor(string[] arguments) =>
        CurlComposition.CreateProxyTunnelOptions(
            CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, DiskDataFileReader.ForProcess, isWindows: false).Options!,
            new SystemSecurityContextFactory());

    private static async Task<List<Http2Frame>> ReadFramesAsync(byte[] bytes)
    {
        using MemoryStream stream = new(bytes);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 24, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>A TLS provider whose handshake returns the plaintext connection agreeing on <c>h2</c>, as an h2 proxy's does.</summary>
    private sealed class H2AgreeingTlsProvider : ITlsProvider
    {
        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(plaintext, null, applicationProtocol: "h2"));
    }
}
