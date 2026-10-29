using System.Buffers.Binary;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Runs <see cref="Tls13ClientConnection" /> and <see cref="Tls12ClientConnection" /> over a
/// pipe to the in-memory servers with a <see cref="TlsHandshakeMessageObserver" /> set, and
/// pins every handshake message and <c>change_cipher_spec</c> it is told of, in wire order.
/// </summary>
[TestClass]
public sealed class TlsHandshakeMessageObserverTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Tls13ConnectAsync_ObserverSet_ReportsEveryMessageFromClientHelloToClientFinished()
    {
        List<ObservedMessage> observed = [];
        Tls13ClientSettings settings = Tls13PipeDriver.DefaultSettings with { SendLegacySessionId = true, HandshakeMessageObserver = Record(observed) };
        Diagnostics.Arrange("settings", "SendLegacySessionId = true, an observer");
        Diagnostics.Arrange("server", "sends a ChangeCipherSpec");

        (Tls13ClientStream client, _, _) = await Tls13PipeDriver.ConnectAsync(settings: settings, serverSendsChangeCipherSpec: true);
        await using Tls13ClientStream stream = client;

        string[] expected =
        [
            "Write Handshake 1 0x0301",
            "Read Handshake 2 0x0303",
            "Read ChangeCipherSpec 1 0x0303",
            "Read Handshake 8 0x0303",
            "Read Handshake 11 0x0303",
            "Read Handshake 15 0x0303",
            "Read Handshake 20 0x0303",
            "Write ChangeCipherSpec 1 0x0303",
            "Write Handshake 20 0x0303",
        ];
        AssertObserved(expected, observed);
    }

    [TestMethod]
    public async Task Tls12ConnectAsync_ObserverSet_ReportsEveryMessageAndBothChangeCipherSpecs()
    {
        List<ObservedMessage> observed = [];
        Tls12ClientSettings settings = Tls12PipeDriver.DefaultSettings with { HandshakeMessageObserver = Record(observed) };
        Diagnostics.Arrange("settings", "an observer");
        Diagnostics.Arrange("server", "handshake records of 100 bytes, so messages span records");

        (Tls12ClientStream client, _, _) = await Tls12PipeDriver.ConnectAsync(settings: settings, handshakeRecordLength: 100);
        await using Tls12ClientStream stream = client;

        string[] expected =
        [
            "Write Handshake 1 0x0301",
            "Read Handshake 2 0x0303",
            "Read Handshake 11 0x0303",
            "Read Handshake 12 0x0303",
            "Read Handshake 14 0x0303",
            "Write Handshake 16 0x0303",
            "Write ChangeCipherSpec 1 0x0303",
            "Write Handshake 20 0x0303",
            "Read ChangeCipherSpec 1 0x0303",
            "Read Handshake 20 0x0303",
        ];
        AssertObserved(expected, observed);
    }

    private static TlsHandshakeMessageObserver Record(List<ObservedMessage> observed) =>
        (direction, contentType, recordVersion, message) => observed.Add(new ObservedMessage(direction, contentType, recordVersion, message.ToArray()));

    // Each handshake message is whole (its header's length is the rest of its bytes) and
    // each change_cipher_spec is the one byte 1; then the order, types and versions are pinned.
    private void AssertObserved(string[] expected, List<ObservedMessage> observed)
    {
        string[] actual = [.. observed.Select(message => message.Describe())];
        Diagnostics.Act("observed", string.Join("; ", actual));
        Diagnostics.Assert("observed", string.Join("; ", expected), string.Join("; ", actual));
        CollectionAssert.AreEqual(expected, actual);
        foreach (ObservedMessage message in observed)
        {
            int expectedLength = message.ContentType == TlsContentType.Handshake ? 4 + (int)(BinaryPrimitives.ReadUInt32BigEndian(message.Bytes) & 0xFFFFFF) : 1;
            Assert.HasCount(expectedLength, message.Bytes, message.Describe());
        }
    }

    private sealed record ObservedMessage(TlsTrafficDirection Direction, TlsContentType ContentType, ushort RecordVersion, byte[] Bytes)
    {
        public string Describe() => $"{Direction} {ContentType} {Bytes[0]} 0x{RecordVersion:x4}";
    }
}
