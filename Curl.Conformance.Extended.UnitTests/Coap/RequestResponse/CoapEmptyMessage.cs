using Curl.Conformance.Extended.Coap;

namespace Curl.Conformance.Extended.UnitTests.Coap.RequestResponse;

/// <summary>Asserts that a datagram curl sent is an Empty message (RFC 7252 section 4.1) of a type and Message ID.</summary>
internal static class CoapEmptyMessage
{
    public static void AssertIs(CoapReceivedDatagram sent, CoapReferenceMessageType type, ushort messageId)
    {
        Assert.HasCount(4, sent.Bytes);
        Assert.AreEqual(type, sent.Message.Type);
        Assert.AreEqual((byte)0, sent.Message.Code);
        Assert.AreEqual(messageId, sent.Message.MessageId);
    }
}
