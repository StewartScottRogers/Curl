using Curl.Testing;
using static Curl.Tls.Tls12PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="Tls12ClientConnection" /> with <see cref="Tls12ClientSettings.KeyLog" /> set:
/// a full handshake and a resumed one each log their master secret once as a
/// <c>CLIENT_RANDOM</c> entry, what curl's OpenSSL build writes to <c>SSLKEYLOGFILE</c> (BL-2327).
/// </summary>
[TestClass]
public sealed class Tls12ClientKeyLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_FullHandshakeWithKeyLog_LogsTheMasterSecretAsClientRandom()
    {
        RecordingKeyLog keyLog = new();

        (Tls12ClientStream client, _, _) = await ConnectAsync(new Tls12TestServer(TestServerCredential.Ed25519()), DefaultSettings with { KeyLog = keyLog });
        await using Tls12ClientStream stream = client;
        Diagnostics.Act("entries", string.Join(" | ", keyLog.Entries.Select(entry => entry.Label)));

        Assert.HasCount(1, keyLog.Entries);
        (string label, byte[] random, byte[] secret) = keyLog.Entries[0];
        Diagnostics.Assert("label", "CLIENT_RANDOM", label);
        Assert.AreEqual("CLIENT_RANDOM", label);
        Assert.HasCount(32, random);
        Assert.HasCount(48, secret);
        CollectionAssert.AreEqual(stream.Handshake.Session!.MasterSecret, secret);
    }

    [TestMethod]
    public async Task ConnectAsync_ResumedHandshakeWithKeyLog_LogsTheResumedMasterSecret()
    {
        Tls12TestSessionCache sessions = new();
        (Tls12ClientStream first, _, _) = await ConnectAsync(new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions });
        Tls12Session session = first.Handshake.Session!;
        await first.DisposeAsync();
        RecordingKeyLog keyLog = new();

        (Tls12ClientStream second, _, _) = await ConnectAsync(
            new Tls12TestServer(TestServerCredential.Ed25519()) { Sessions = sessions },
            DefaultSettings with { SessionToResume = session, KeyLog = keyLog });
        await using Tls12ClientStream stream = second;
        Diagnostics.Act("resumed", stream.Handshake.IsResumed);

        Assert.IsTrue(stream.Handshake.IsResumed);
        Assert.HasCount(1, keyLog.Entries);
        Assert.AreEqual("CLIENT_RANDOM", keyLog.Entries[0].Label);
        CollectionAssert.AreEqual(session.MasterSecret, keyLog.Entries[0].Secret);
    }

    private sealed class RecordingKeyLog : ITlsKeyLog
    {
        public List<(string Label, byte[] ClientRandom, byte[] Secret)> Entries { get; } = [];

        public void Append(string label, ReadOnlySpan<byte> clientRandom, ReadOnlySpan<byte> secret) =>
            Entries.Add((label, clientRandom.ToArray(), secret.ToArray()));
    }
}
