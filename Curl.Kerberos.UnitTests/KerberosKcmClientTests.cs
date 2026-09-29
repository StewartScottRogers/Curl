namespace Curl.Kerberos;

/// <summary>
/// Pins <see cref="KerberosKcmClient" /> to MIT <c>cc_kcm.c</c>'s framing: a 4-byte
/// big-endian length, version 2.0, the 2-byte opcode and the arguments out; a 4-byte
/// big-endian length, a 4-byte status and the payload back.
/// </summary>
[TestClass]
public sealed class KerberosKcmClientTests
{
    [TestMethod]
    public void Call_WritesTheFramedRequestAndReadsTheReply()
    {
        FakeKcm kcm = new FakeKcm().Reply([0x61, 0x62]);
        KerberosKcmClient client = new(kcm.Connect("/socket")!);

        KerberosKcmReply reply = client.Call(KerberosKcmOperation.GetCredentialByUuid, [0x31, 0x00], [0xAA, 0xBB]);

        Assert.IsTrue(reply.IsSuccess);
        Assert.AreEqual(0, reply.Status);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62 }, reply.Payload);
        CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 10, 0x31, 0x00, 0xAA, 0xBB }, kcm.Connection.Requests().Single());
        Assert.AreEqual(1, kcm.Connection.FlushCount);
    }

    [TestMethod]
    public void Call_NonZeroStatus_IsAReplyNotAnException()
    {
        FakeKcm kcm = new FakeKcm().Reply(-1765328243, []);

        KerberosKcmReply reply = new KerberosKcmClient(kcm.Connect("/socket")!).Call(KerberosKcmOperation.GetDefaultCache);

        Assert.IsFalse(reply.IsSuccess);
        Assert.AreEqual(-1765328243, reply.Status);
        Assert.IsEmpty(reply.Payload);
        CollectionAssert.AreEqual(new byte[] { 2, 0, 0, 20 }, kcm.Connection.Requests().Single());
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 3, 0, 0, 0 }, DisplayName = "shorter than a status code")]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, DisplayName = "negative length")]
    [DataRow(new byte[] { 0, 0xA0, 0, 1 }, DisplayName = "one byte over 10 MiB")]
    public void Call_ReplyLengthOutOfRange_FailsAsMalformed(byte[] reply)
    {
        FakeKcm kcm = new FakeKcm().RawReply(reply);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => new KerberosKcmClient(kcm.Connect("/socket")!).Call(KerberosKcmOperation.GetDefaultCache));

        Assert.AreEqual(KerberosFileError.KcmReplyMalformed, failure.Error);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0 }, DisplayName = "inside the length")]
    [DataRow(new byte[] { 0, 0, 0, 8, 0, 0, 0, 0, 1 }, DisplayName = "inside the payload")]
    public void Call_ConnectionClosesInsideTheReply_FailsAsTruncated(byte[] reply)
    {
        FakeKcm kcm = new FakeKcm().RawReply(reply);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => new KerberosKcmClient(kcm.Connect("/socket")!).Call(KerberosKcmOperation.GetDefaultCache));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
    }
}
