using Curl.Ssh.Fakes;
using Curl.Ssh.KeyExchange;
using Curl.Ssh.Negotiation;
using Curl.Testing;

namespace Curl.Ssh.Transport;

public sealed partial class SshTransportTests
{
    [TestMethod]
    public async Task StartDelayedCompression_AfterAnExchangeThatAgreedZlibAtOpenSshBothWays_CompressesTheClientsPackets()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string method = "curve25519-sha256";
        TestHostKey hostKey = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        SshAlgorithmPreferences preset = SshAlgorithmPreferences.Full.WithCompression(true);
        SshAlgorithmCatalogue catalogue = new(
            preset.KeyExchange.Concat(preset.ServerHostKey).Concat(preset.Cipher).Concat(preset.Mac).Concat(preset.Compression));
        SshKexInit server = ServerKexInit(method, hostKey.Algorithm, strict: true) with
        {
            CompressionClientToServer = ["zlib@openssh.com"],
            CompressionServerToClient = ["zlib@openssh.com"],
        };
        TestEphemeralKeys keys = new();
        byte[] clientKexInit = SshKexInit.ForClient(preset, catalogue, new RepeatingRandomSource(0x33)).ToPayload();
        TestKeyExchangeServer answer = TestKeyExchangeServer.Answer(method, hostKey, keys, clientKexInit, server.ToPayload(), preset.GroupExchangeSizes, null);
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(server);
        answer.ServerPayloads.Select(payload => payload.ToArray()).ToList().ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys);
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, preset, catalogue, new RepeatingRandomSource(0x33), keys);
        diagnostics.Arrange("key exchange method", method);
        diagnostics.Arrange("server compression both ways", "zlib@openssh.com");

        SshKeyExchangeResult result = await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        byte[] payload = new byte[4096];
        int beforeUncompressed = connection.Written.Length;
        await transport.PacketWriter.WriteAsync(payload, CancellationToken.None);
        int uncompressedLength = connection.Written.Length - beforeUncompressed;
        transport.StartDelayedCompression();
        int beforeCompressed = connection.Written.Length;
        await transport.PacketWriter.WriteAsync(payload, CancellationToken.None);
        int compressedLength = connection.Written.Length - beforeCompressed;

        diagnostics.Act("compression client to server", result.Algorithms.CompressionClientToServer);
        diagnostics.Act("compression server to client", result.Algorithms.CompressionServerToClient);
        diagnostics.Act("packet length before StartDelayedCompression", uncompressedLength);
        diagnostics.Act("packet length after StartDelayedCompression", compressedLength);
        diagnostics.Assert("compression client to server", "zlib@openssh.com", result.Algorithms.CompressionClientToServer);
        diagnostics.Assert("compression server to client", "zlib@openssh.com", result.Algorithms.CompressionServerToClient);
        Assert.AreEqual("zlib@openssh.com", result.Algorithms.CompressionClientToServer);
        Assert.AreEqual("zlib@openssh.com", result.Algorithms.CompressionServerToClient);
        Assert.IsGreaterThan(payload.Length, uncompressedLength, "the exchange alone leaves zlib@openssh.com waiting");
        Assert.IsLessThan(payload.Length / 4, compressedLength, "4096 zero bytes compress to far less once compression starts");
    }
}
