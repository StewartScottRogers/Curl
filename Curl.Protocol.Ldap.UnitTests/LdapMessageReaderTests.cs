using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="LdapMessageReader" />: a whole LDAPMessage however its bytes are split
/// across reads, messages that share a read, and what a close or bytes that cannot start a
/// message report.
/// </summary>
[TestClass]
public sealed class LdapMessageReaderTests
{
    private static readonly byte[] BindSuccess = Hex.Bytes("30 0c 02 01 01 61 07 0a 01 00 04 00 04 00");

    [TestMethod]
    [DataRow("", LdapMessageReader.NeedMoreBytes)]
    [DataRow("30", LdapMessageReader.NeedMoreBytes)]
    [DataRow("04", LdapMessageReader.MalformedFrame)]
    [DataRow("04 00", LdapMessageReader.MalformedFrame)]
    [DataRow("30 00", 2)]
    [DataRow("30 0c", 14)]
    [DataRow("30 7f", 129)]
    [DataRow("30 80", LdapMessageReader.MalformedFrame)]
    [DataRow("30 85 00 00 00 00 01", LdapMessageReader.MalformedFrame)]
    [DataRow("30 84 00 00 00", LdapMessageReader.NeedMoreBytes)]
    [DataRow("30 84 00 00 00 1f", 37)]
    [DataRow("30 81 c8", 203)]
    [DataRow("30 82 01 00", 260)]
    [DataRow("30 84 ff ff ff ff", LdapMessageReader.MalformedFrame)]
    public void MeasureFrame_ReturnsTheWholeLengthOrWhyNot(string bytes, int expected)
    {
        Assert.AreEqual(expected, LdapMessageReader.MeasureFrame(Hex.Bytes(bytes)));
    }

    [TestMethod]
    public async Task ReadMessageAsync_WholeMessageInOneRead_ReturnsIt()
    {
        var reader = new LdapMessageReader(new ScriptedConnection(BindSuccess));

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(BindSuccess, message);
    }

    [TestMethod]
    public async Task ReadMessageAsync_OneByteAtATime_ReturnsTheWholeMessage()
    {
        byte[][] reads = [.. BindSuccess.Select(octet => new[] { octet })];
        var reader = new LdapMessageReader(new ScriptedConnection(reads));

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(BindSuccess, message);
    }

    [TestMethod]
    public async Task ReadMessageAsync_SplitInsideALongFormLength_ReturnsTheWholeMessage()
    {
        byte[] winLdapStyle = Hex.Bytes("30 84 00 00 00 0c 02 01 01 61 84 00 00 00 03 0a 01 00");
        var reader = new LdapMessageReader(new ScriptedConnection(winLdapStyle[..3], winLdapStyle[3..10], winLdapStyle[10..]));

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(winLdapStyle, message);
    }

    [TestMethod]
    public async Task ReadMessageAsync_TwoMessagesInOneRead_ReturnsThemInOrder()
    {
        byte[] second = Hex.Bytes("30 0c 02 01 02 61 07 0a 01 31 04 00 04 00");
        var reader = new LdapMessageReader(new ScriptedConnection([.. BindSuccess, .. second]));

        (_, byte[] first) = await reader.ReadMessageAsync(CancellationToken.None);
        (LdapReadStatus status, byte[] next) = await reader.ReadMessageAsync(CancellationToken.None);

        CollectionAssert.AreEqual(BindSuccess, first);
        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(second, next);
    }

    [TestMethod]
    public async Task ReadMessageAsync_MessageLargerThanTheBuffer_GrowsTheBuffer()
    {
        byte[] large = [0x30, 0x82, 0x27, 0x10, .. new byte[10000]];
        var reader = new LdapMessageReader(new ScriptedConnection(large));

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Message, status);
        CollectionAssert.AreEqual(large, message);
    }

    [TestMethod]
    public async Task ReadMessageAsync_ServerClosesFirst_ReturnsClosed()
    {
        var reader = new LdapMessageReader(new ScriptedConnection());

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Closed, status);
        Assert.IsEmpty(message);
    }

    [TestMethod]
    public async Task ReadMessageAsync_ServerClosesMidMessage_ReturnsClosed()
    {
        var reader = new LdapMessageReader(new ScriptedConnection(BindSuccess[..5]));

        (LdapReadStatus status, _) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Closed, status);
    }

    [TestMethod]
    public async Task ReadMessageAsync_NotASequence_ReturnsMalformed()
    {
        var reader = new LdapMessageReader(new ScriptedConnection(Hex.Bytes("48 54 54 50")));

        (LdapReadStatus status, byte[] message) = await reader.ReadMessageAsync(CancellationToken.None);

        Assert.AreEqual(LdapReadStatus.Malformed, status);
        Assert.IsEmpty(message);
    }
}
