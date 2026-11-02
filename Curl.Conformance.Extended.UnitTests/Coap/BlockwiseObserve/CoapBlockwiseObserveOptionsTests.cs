using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.BlockwiseObserve;

namespace Curl.Conformance.Extended.UnitTests.Coap.BlockwiseObserve;

[TestClass]
public sealed class CoapBlockwiseObserveOptionsTests
{
    [TestMethod]
    public void Uint_Zero_HasAnEmptyValue()
    {
        CoapReferenceOption option = CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.Observe, 0);

        Assert.AreEqual(CoapBlockwiseObserveOptions.Observe, option.Number);
        Assert.IsEmpty(option.Value);
    }

    [TestMethod]
    public void Uint_MultiByteValue_IsBigEndianInAsFewBytesAsPossible()
    {
        CoapReferenceOption option = CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.Size1, 0x01_0203);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, option.Value);
    }

    [TestMethod]
    public void UintOf_ValueWithLeadingZeroBytes_IsRead()
    {
        CoapReferenceMessage message = MessageWith(new CoapReferenceOption(CoapBlockwiseObserveOptions.MaxAge, [0, 0, 1, 44]));

        Assert.AreEqual(300u, CoapBlockwiseObserveOptions.UintOf(message, CoapBlockwiseObserveOptions.MaxAge));
    }

    [TestMethod]
    public void UintOf_MissingOption_IsNull()
    {
        Assert.IsNull(CoapBlockwiseObserveOptions.UintOf(MessageWith(), CoapBlockwiseObserveOptions.Size2));
    }

    [TestMethod]
    public void BlockOf_BlockOption_ReadsNumberMoreAndSizeExponent()
    {
        CoapReferenceBlock block = new(5, true, 2);
        CoapReferenceMessage message = MessageWith(CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block1, block));

        CoapReferenceBlock? read = CoapBlockwiseObserveOptions.BlockOf(message, CoapBlockwiseObserveOptions.Block1);

        Assert.AreEqual(block, read);
        Assert.AreEqual(64, read!.Size);
        Assert.AreEqual(320, read.Offset);
        Assert.AreEqual(0x5Au, block.ToUint());
    }

    [TestMethod]
    public void BlockOf_MissingOption_IsNull()
    {
        Assert.IsNull(CoapBlockwiseObserveOptions.BlockOf(MessageWith(), CoapBlockwiseObserveOptions.Block2));
    }

    [TestMethod]
    public void FromUint_MBitClear_IsNotMore()
    {
        Assert.AreEqual(new CoapReferenceBlock(1, false, 6), CoapReferenceBlock.FromUint(0x16));
    }

    [TestMethod]
    public void Block_NullBlock_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapBlockwiseObserveOptions.Block(CoapBlockwiseObserveOptions.Block2, null!));
    }

    [TestMethod]
    public void UintOf_NullMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapBlockwiseObserveOptions.UintOf(null!, CoapBlockwiseObserveOptions.Block2));
    }

    [TestMethod]
    public void Notification_CarriesTheRegistrationTokenAndItsOwnMessageId()
    {
        CoapReferenceMessage registration = new(1, CoapReferenceMessageType.Confirmable, 1, 7, [9, 8], [], []);

        CoapReferenceMessage notification = CoapReferenceMessage.Decode(
            CoapNotifications.Notification(registration, CoapReferenceMessageType.NonConfirmable, 0x1234, CoapResponses.Content, [65], CoapBlockwiseObserveOptions.Uint(CoapBlockwiseObserveOptions.Observe, 3)));

        Assert.AreEqual(CoapReferenceMessageType.NonConfirmable, notification.Type);
        Assert.AreEqual((ushort)0x1234, notification.MessageId);
        CollectionAssert.AreEqual(registration.Token, notification.Token);
        Assert.AreEqual(3u, CoapBlockwiseObserveOptions.UintOf(notification, CoapBlockwiseObserveOptions.Observe));
        CollectionAssert.AreEqual(new byte[] { 65 }, notification.Payload);
    }

    [TestMethod]
    public void Notification_NullRegistration_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapNotifications.Notification(null!, CoapReferenceMessageType.NonConfirmable, 1, CoapResponses.Content, []));
    }

    private static CoapReferenceMessage MessageWith(params IReadOnlyList<CoapReferenceOption> options) =>
        new(1, CoapReferenceMessageType.Acknowledgement, CoapResponses.Content, 1, [], options, []);
}
