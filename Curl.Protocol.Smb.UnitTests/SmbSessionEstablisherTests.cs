using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbSessionEstablisher" />'s outcomes beyond the handler's: the UID it
/// takes, a short negotiate response (exit 7), a receive error in either reply (exit 56)
/// and a session setup too large for curl's 1024-byte buffer (exit 63).
/// </summary>
[TestClass]
public sealed class SmbSessionEstablisherTests
{
    private static readonly SmbIdentity Identity = new("User", SmbRecordedExchange.Host);

    [TestMethod]
    public async Task EstablishAsync_AcceptedSession_ReturnsTheUid()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupAccepted);

        (ushort userId, TransferResult? failure) = await Establisher(connection).EstablishAsync("Password", Identity, CancellationToken.None);

        Assert.AreEqual((ushort)0x0064, userId);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public async Task EstablishAsync_NegotiateResponseOneByteShortOfTheChallenge_Exits7()
    {
        byte[] response = SmbRecordedExchange.NegotiateResponse;
        response[3]--;
        response[71]--;
        var connection = new ScriptedConnection(response[..^1]);

        (_, TransferResult? failure) = await Establisher(connection).EstablishAsync("Password", Identity, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure!.ExitCode);
    }

    [TestMethod]
    public async Task EstablishAsync_MalformedNegotiateResponse_Exits56()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.Hex("00 00 00 00"));

        (_, TransferResult? failure) = await Establisher(connection).EstablishAsync("Password", Identity, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, failure!.ExitCode);
        Assert.AreEqual("too small NetBIOS frame size 4", failure.ErrorMessage);
    }

    [TestMethod]
    public async Task EstablishAsync_MalformedSessionSetupResponse_Exits56()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.Hex("00 00 ff ff"));

        (_, TransferResult? failure) = await Establisher(connection).EstablishAsync("Password", Identity, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, failure!.ExitCode);
        Assert.AreEqual("too large NetBIOS frame size 65539", failure.ErrorMessage);
    }

    [TestMethod]
    public async Task EstablishAsync_SessionSetupPastCurlsBuffer_Exits63WithoutSendingIt()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse);
        var identity = new SmbIdentity(new string('u', 949), "d");

        (_, TransferResult? failure) = await Establisher(connection).EstablishAsync("Password", identity, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, failure!.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", failure.ErrorMessage);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, connection.Sent);
    }

    private static SmbSessionEstablisher Establisher(ScriptedConnection connection) =>
        new(connection, new SmbMessageReader(connection, TimeProvider.System), SmbCurlOperatingSystem.Linux);
}
