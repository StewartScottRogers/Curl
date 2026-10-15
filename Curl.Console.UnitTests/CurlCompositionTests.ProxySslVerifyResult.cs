using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Cli;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

// Pins the proxy's TLS verify results reaching the run's -w variables: the one part of the HTTPS proxy
// handshake tests (EngineCompositionTests.ProxyPinnedPublicKey) that records into the tool's own
// RunningTransferState, so it stays with Curl.Console.
public sealed partial class CurlCompositionTests
{
    private const string VerifyResultProxyUrl = "https://127.0.0.1:47606";

    private const string VerifyResultProxiedUrl = "http://example.com/";
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CreateTransports_SelfSignedProxyUnderProxyInsecure_RecordsProxySslVerifyResult18OffWindows()
    {
        // curl -s -o /dev/null --proxy-insecure -x https://localhost:18443 -w '%{ssl_verify_result} %{proxy_ssl_verify_result}'
        // http://example.invalid/ -> "0 18" (curl 8.18.0, OpenSSL 3.5.5, 2026-09-30, BL-661 Notes).
        Diagnostics.Arrange("proxy tls arguments", "--proxy-insecure only");
        RunningTransferState state = NewRunningTransferState();

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await LoopbackProxyHandshakeAsync(_ => [], new TlsResultRecordingTransferEvents(NoTransferEvents.Instance, state));
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("proxy ssl verify result", state.ProxySslVerifyResult);
        Diagnostics.Act("ssl verify result", state.SslVerifyResult);
        Diagnostics.Assert("proxy ssl verify result", 18L, state.ProxySslVerifyResult);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(18L, state.ProxySslVerifyResult);
        Assert.AreEqual(0L, state.SslVerifyResult);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateTransports_SelfSignedProxyUnderProxyInsecure_RecordsProxySslVerifyResult0OnWindows()
    {
        // curl 8.21.0 (Schannel) prints 0 for both, whatever it found (ADR-0043).
        Diagnostics.Arrange("proxy tls arguments", "--proxy-insecure only");
        RunningTransferState state = NewRunningTransferState();

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await LoopbackProxyHandshakeAsync(_ => [], new TlsResultRecordingTransferEvents(NoTransferEvents.Instance, state));
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("ssl verify result", state.SslVerifyResult);
        Diagnostics.Assert("proxy ssl verify result", 0L, state.ProxySslVerifyResult);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(0L, state.ProxySslVerifyResult);
        Assert.AreEqual(0L, state.SslVerifyResult);
        await result.Connection!.DisposeAsync();
    }

    // Runs the HTTPS proxy's handshake against a loopback TLS server with a throwaway self-signed key.
    private static async Task<ConnectResult> LoopbackProxyHandshakeAsync(Func<byte[], string[]> proxyTlsArguments, ITransferEvents events)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Task serverTask = ServeOneHandshakeAsync(listener, certificate);

        CommandLineOptions options = Parse(["-x", VerifyResultProxyUrl, "--proxy-insecure", .. proxyTlsArguments(key.ExportSubjectPublicKeyInfo()), VerifyResultProxiedUrl]);
        CurlTransports transports = EngineComposition.CreateTransports(options, EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.TlsOf(options), EasyOptionsFromCommandLine.HttpOf(options), EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options));
        TcpClient client = new();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        ConnectResult result = await ((SslStreamTlsProvider)transports.ProxyTlsProvider).AuthenticateAsClientAsync(
            new StreamConnection(client.GetStream(), listener.LocalEndpoint), "127.0.0.1", events, isProxy: true, CancellationToken.None);

        if (result.Connection is null)
        {
            client.Dispose();
        }

        try
        {
            await serverTask;
        }
        catch (Exception exception) when (exception is IOException or AuthenticationException or SocketException)
        {
            // The client closed the connection on a failed pin.
        }

        return result;
    }

    private static RunningTransferState NewRunningTransferState()
    {
        StandardOutputFailureDeferringStream standardOutput = new(new MemoryStream());
        return new RunningTransferState(0, Array.Empty<CommandLineOptions>(), CancellationToken.None, standardOutput, standardOutput);
    }

    private static async Task ServeOneHandshakeAsync(TcpListener listener, X509Certificate2 certificate)
    {
        using TcpClient accepted = await listener.AcceptTcpClientAsync();
        await using SslStream server = new(accepted.GetStream());
        await server.AuthenticateAsServerAsync(certificate);
    }
}
