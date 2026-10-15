using Curl.Testing;
using static Curl.Tls.Tls13PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="Tls13ClientConnection" /> with <see cref="Tls13ClientSettings.KeyLog" /> set:
/// a handshake logs its five secrets in OpenSSL's order, each with the ClientHello's random, what
/// curl's OpenSSL build writes to <c>SSLKEYLOGFILE</c> (BL-2339).
/// </summary>
[TestClass]
public sealed class Tls13ClientKeyLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_FullHandshakeWithKeyLog_LogsTheFiveSecretsInOpenSslOrder()
    {
        RecordingKeyLog keyLog = new();
        Tls13TestServer testServer = new(TestServerCredential.Ed25519());

        (Tls13ClientStream client, _, _) = await ConnectAsync(testServer, DefaultSettings with { KeyLog = keyLog });
        await using Tls13ClientStream stream = client;
        Diagnostics.Act("labels", string.Join(" | ", keyLog.Entries.Select(entry => entry.Label)));

        string[] expectedLabels =
        [
            "CLIENT_HANDSHAKE_TRAFFIC_SECRET",
            "SERVER_HANDSHAKE_TRAFFIC_SECRET",
            "CLIENT_TRAFFIC_SECRET_0",
            "SERVER_TRAFFIC_SECRET_0",
            "EXPORTER_SECRET",
        ];
        CollectionAssert.AreEqual(expectedLabels, keyLog.Entries.Select(entry => entry.Label).ToArray());
        byte[][] expectedSecrets =
        [
            testServer.ClientHandshakeTrafficSecret,
            testServer.ServerHandshakeTrafficSecret,
            testServer.ClientApplicationTrafficSecret,
            testServer.ServerApplicationTrafficSecret,
            stream.Handshake.ExporterMasterSecret!,
        ];
        for (int index = 0; index < expectedSecrets.Length; index++)
        {
            CollectionAssert.AreEqual(expectedSecrets[index], keyLog.Entries[index].Secret, expectedLabels[index]);
            CollectionAssert.AreEqual(stream.Handshake.SentClientHello!.Random, keyLog.Entries[index].ClientRandom, expectedLabels[index]);
        }
    }

    /// <summary>Keeps every entry handed to it, in order.</summary>
    internal sealed class RecordingKeyLog : ITlsKeyLog
    {
        public List<(string Label, byte[] ClientRandom, byte[] Secret)> Entries { get; } = [];

        public void Append(string label, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> secret) =>
            Entries.Add((label, clientRandom.ToArray(), secret.ToArray()));
    }
}
