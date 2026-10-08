using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Attacks <see cref="SshProtocolHandler" /> with a hostile server before key exchange
/// finishes (BL-1518, <c>Documentation/Wiki/Adversarial-Testing.md</c>): oversized and
/// malformed packets, a <c>KEXINIT</c> nothing agrees with, a message sent in the wrong
/// state and bytes delivered one at a time. Every one must end as libssh2's handshake
/// failures do in curl - exit 2 and "Failure establishing ssh session" - never with an
/// exception escaping the handler.
/// </summary>
[TestClass]
public sealed class SshProtocolHandlerAdversarialTests
{
    private const string Banner = "SSH-2.0-OpenSSH_9.6\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0xFFFFFFFFu, DisplayName = "uint32 maximum")]
    [DataRow(0x00009C48u, DisplayName = "40008, one block past the largest")]
    [DataRow(0u, DisplayName = "zero")]
    [DataRow(13u, DisplayName = "not block-aligned")]
    public async Task ExecuteAsync_FirstPacketLengthOutsideTheValidRange_FailsTheSessionWithExit2(uint packetLength)
    {
        byte[] packet = [.. SshTestEncoding.UInt32(packetLength), 4, 20, .. new byte[16]];

        TransferResult result = await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet);

        AssertSessionFailed(result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerKexInitWithEveryNameListEmpty_FailsTheSessionWithExit2()
    {
        byte[][] fields = [[20], new byte[16], .. Enumerable.Repeat(SshTestEncoding.String([]), 10), [0], SshTestEncoding.UInt32(0)];
        byte[] packet = new SshServerScript().Packet(SshTestEncoding.Join(fields)).Bytes;

        TransferResult result = await RunAgainst("scp", Encoding.ASCII.GetBytes(Banner), packet);

        AssertSessionFailed(result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerKexInitWithOnlyStrayCommas_FailsTheSessionWithExit2()
    {
        byte[][] fields = [[20], new byte[16], .. Enumerable.Repeat(SshTestEncoding.Name(",,"), 10), [0], SshTestEncoding.UInt32(0)];
        byte[] packet = new SshServerScript().Packet(SshTestEncoding.Join(fields)).Bytes;

        TransferResult result = await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet);

        AssertSessionFailed(result);
    }

    [TestMethod]
    [DataRow((byte)52, DisplayName = "USERAUTH_SUCCESS before key exchange")]
    [DataRow((byte)94, DisplayName = "CHANNEL_DATA before key exchange")]
    [DataRow((byte)21, DisplayName = "NEWKEYS before KEXINIT")]
    [DataRow((byte)31, DisplayName = "KEX reply before KEXINIT")]
    public async Task ExecuteAsync_MessageValidOnlyAfterKeyExchangeSentBeforeIt_FailsTheSessionWithExit2(byte messageNumber)
    {
        byte[] packet = new SshServerScript().Packet(messageNumber, 0, 0, 0, 0).Bytes;

        TransferResult result = await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet);

        AssertSessionFailed(result);
    }

    [TestMethod]
    public async Task ExecuteAsync_KexInitCutShortInsideItsPacket_FailsTheSessionWithExit2()
    {
        byte[] packet = new SshServerScript().KexInit(SshServerScript.OpenSshKexInit()).Bytes;

        TransferResult result = await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet[..(packet.Length / 2)]);

        AssertSessionFailed(result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BannerAndPacketsDeliveredOneByteAtATime_EndTheSameAsDeliveredWhole()
    {
        byte[] wire = [.. Encoding.ASCII.GetBytes(Banner), .. SshTestEncoding.UInt32(0xFFFFFFFF), 4, 20];
        TransferResult whole = await RunAgainst("sftp", wire);
        Diagnostics.Arrange("whole delivery", whole);

        TransferResult byteByByte = await RunAgainst("sftp", [.. wire.Select(value => new[] { value })]);

        Diagnostics.Assert("one byte per read", whole, byteByByte);
        Assert.AreEqual(whole, byteByByte);
        AssertSessionFailed(byteByByte);
    }

    [TestMethod]
    [DataRow("SSH-2.0-OpenSSH_9.6", DisplayName = "no line ending, then the end")]
    [DataRow("\r\n\r\n\r\n", DisplayName = "only empty lines")]
    [DataRow("HTTP/1.1 400 Bad Request\r\n\r\n", DisplayName = "an HTTP server")]
    [DataRow("SSH-\r\n", DisplayName = "an identification with nothing after SSH-")]
    public async Task ExecuteAsync_ServerIdentificationMalformedThenTheEnd_FailsTheSessionWithExit2(string sent)
    {
        TransferResult result = await RunAgainst("scp", Encoding.ASCII.GetBytes(sent));

        AssertSessionFailed(result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHostileServerOnEightTransfersAtOnce_EveryOneFailsTheSame()
    {
        byte[] packet = [.. SshTestEncoding.UInt32(0xFFFFFFFF), 4, 20];
        TransferResult alone = await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet);
        Diagnostics.Arrange("one transfer alone", alone);

        TransferResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () => await RunAgainst("sftp", Encoding.ASCII.GetBytes(Banner), packet))));

        Diagnostics.Assert("eight at once", $"{alone} x8", string.Join(" | ", results.Distinct()));
        Assert.IsTrue(results.All(result => result == alone));
    }

    private async Task<TransferResult> RunAgainst(string scheme, params byte[][] reads)
    {
        SshProtocolHandler handler = new(
            new ScriptedConnector(reads),
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.WindowsReference,
            Encoding.UTF8);
        Diagnostics.Arrange("server sends", $"{reads.Sum(read => read.Length)} bytes in {reads.Length} reads, then closes");
        Diagnostics.Bytes("server bytes", [.. reads.SelectMany(read => read)]);

        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse($"{scheme}://files.example/f"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("tester", "secret"),
        });

        Diagnostics.Act("result", result);
        return result;
    }

    private void AssertSessionFailed(TransferResult result)
    {
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.ExitCode);
        Diagnostics.Assert("error starts", "Failure establishing ssh session: ", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.StartsWith("Failure establishing ssh session: ", result.ErrorMessage ?? string.Empty);
    }

    private sealed class ScriptedConnector(byte[][] reads) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(new ScriptedConnection(reads)));
    }
}
