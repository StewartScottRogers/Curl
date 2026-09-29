using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The QUIC steps <see cref="TcpConnector.ConnectMultiplexedAsync" /> writes to the target's
/// diagnostic log around the <see cref="QuicDialer" /> (ADR-0222, BL-920).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHandshakeCompletes_LogsTheDialAtVerboseAndTheConnectionAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target() with { DiagnosticLog = log }, CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(new[] { "dialling quic.test:443 over QUIC at 127.0.0.1" }, log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Quic));
        CollectionAssert.AreEqual(new[] { "QUIC connection to quic.test:443 established" }, log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Quic));
        CollectionAssert.AreEqual(new[] { "quic.test:443 resolved by lookup to 127.0.0.1 in 0 ms" }, log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AtWarning_LogsNothingForAConnectionMade()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(Target() with { DiagnosticLog = log }, CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHostDoesNotResolve_LogsTheFailureAtError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider(), resolver: new FakeDnsResolver());

        await connector.ConnectMultiplexedAsync(Target() with { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { (DiagnosticLogLevel.Error, DiagnosticLogComponents.Quic, "failed with CouldntResolveHost (6): Could not resolve host: quic.test") },
            log.Lines);
    }
}
