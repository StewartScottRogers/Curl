using System.Text;
using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the Console passes the certificate checks an option group's extension options activate down to the
/// engine (ADR-1016): a transfer whose check refuses the server's chain exits 60 with the check's message, one whose
/// check lets it stand runs as before, and a transfer without the option keeps curl's connector and TLS provider.
/// </summary>
[TestClass]
public sealed class ExtensionCertificateCheckHookTests
{
    private static readonly ExtensionOption DaneOption = new("test-dane", ExtensionOptionArgument.None, "Test DANE");
    private static readonly ExtensionOption PinOption = new("test-pin", ExtensionOptionArgument.Value, "Test pin");
    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> Chain = [new byte[] { 0x30, 0x03, 0x02, 0x01, 0x01 }];

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ncurl")]);
    private readonly RecordingDatagramConnector curlDatagrams = new(CurlExitCode.CouldntConnect, "unused");
    private readonly List<string> checkedHosts = [];

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ExtensionCheckRefusesTheChain_ExitsSixtyWithTheChecksMessage()
    {
        int exitCode = await RunAsync("-sS", "--test-dane", "https://example.test:20603/");

        Assert.AreEqual(60, exitCode);
        Assert.StartsWith("curl: (60) TLSA records do not match the certificate\nMore details here: https://curl.se/docs/sslcerts.html\n", StandardErrorText.ReplaceLineEndings("\n"));
        Assert.AreEqual(0, standardOutput.Length);
        Assert.IsEmpty(server.Written);
        CollectionAssert.AreEqual(new[] { "example.test" }, checkedHosts);
    }

    [TestMethod]
    public async Task RunAsync_ExtensionCheckLetsTheChainStand_TransfersAsBefore()
    {
        int exitCode = await RunAsync("-sS", "--test-pin", "ok", "https://example.test:20603/");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("curl", Encoding.UTF8.GetString(standardOutput.ToArray()));
        CollectionAssert.AreEqual(new[] { "example.test" }, checkedHosts);
    }

    [TestMethod]
    public async Task RunAsync_NoExtensionOptionGiven_TransfersWithoutAnyCheck()
    {
        int exitCode = await RunAsync("-sS", "https://example.test:20603/");

        Assert.AreEqual(0, exitCode, StandardErrorText);
        Assert.AreEqual("curl", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.IsEmpty(checkedHosts);
    }

    [TestMethod]
    public void ExtensionCertificateCheckHooksOf_NoExtensionOptionGiven_PassesCurlsConnectorAndTlsProviderUnchanged()
    {
        CertificateCheckHooks hooks = CurlComposition.ExtensionCertificateCheckHooksOf(new CommandLineOptions(), [ActivatedByDane, ActivatedByPin]);

        Assert.IsEmpty(hooks.Hooks);
        Assert.IsNull(CurlComposition.ExtensionCertificateConnectorOf(hooks));
        Assert.IsNull(CurlComposition.ExtensionTlsProviderOf(hooks));
        Assert.IsNull(CurlComposition.ExtensionCertificateConnectorOf(null));
        Assert.IsNull(CurlComposition.ExtensionTlsProviderOf(null));
        Assert.IsEmpty(CurlComposition.ExtensionCertificateCheckHooksOf(new CommandLineOptions()).Hooks);
    }

    [TestMethod]
    public void ExtensionTlsProviderOf_CheckActivated_GivesTheChecksTlsProviderAndConnector()
    {
        CertificateCheckHooks hooks = CurlComposition.ExtensionCertificateCheckHooksOf(new CommandLineOptions(), [_ => new RecordingCheck(DaneOption, null, checkedHosts)]);
        ITlsProvider curlTlsProvider = EngineComposition.CreateTlsProvider(new TlsClientOptions(), TimeProvider.System);

        Assert.AreNotSame(curlTlsProvider, CurlComposition.ExtensionTlsProviderOf(hooks)!(curlTlsProvider));
        Assert.AreNotSame(server, CurlComposition.ExtensionCertificateConnectorOf(hooks)!(server));
    }

    private ICertificateCheckHook? ActivatedByDane(ExtensionOptionValues values) =>
        values.ValueOf(DaneOption) is not null ? new RecordingCheck(DaneOption, "TLSA records do not match the certificate", checkedHosts) : null;

    private ICertificateCheckHook? ActivatedByPin(ExtensionOptionValues values) =>
        values.ValueOf(PinOption) is not null ? new RecordingCheck(PinOption, null, checkedHosts) : null;

    private Task<int> RunAsync(params string[] arguments) =>
        CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                new ChainReportingConnector(server),
                curlDatagrams,
                extensionOptions: [DaneOption, PinOption],
                extensionCertificateCheckHookActivators: [ActivatedByDane, ActivatedByPin])
            .RunAsync(arguments);

    private sealed class RecordingCheck(ExtensionOption option, string? refusal, List<string> checkedHosts) : ICertificateCheckHook
    {
        public ExtensionOption Option => option;

        public ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, int? port, CancellationToken cancellationToken)
        {
            checkedHosts.Add(host);
            return ValueTask.FromResult(refusal);
        }
    }

    // Reports the chain a TLS connect curl accepted would, over the scripted server's connection.
    private sealed class ChainReportingConnector(ScriptedConnector inner) : IConnector
    {
        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ConnectResult.Connected((await inner.ConnectAsync(target, cancellationToken)).Connection!, null, peerCertificates: Chain);
    }
}
