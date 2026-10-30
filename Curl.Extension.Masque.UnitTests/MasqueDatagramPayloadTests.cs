using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque;

/// <summary>Checks the Context ID framing of RFC 9298 section 5 and RFC 9484 section 6.</summary>
[TestClass]
public sealed class MasqueDatagramPayloadTests
{
    [TestMethod]
    public void Wrap_PrefixesContextIdZero() =>
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, MasqueDatagramPayload.Wrap([1, 2]));

    [TestMethod]
    [DataRow(new byte[] { 0, 1, 2 })]
    [DataRow(new byte[] { 0x40, 0, 1, 2 })]
    public void TryUnwrap_ContextIdZero_GivesTheContent(byte[] payload)
    {
        Assert.IsTrue(MasqueDatagramPayload.TryUnwrap(payload, out var content));
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, content.ToArray());
    }

    [TestMethod]
    [DataRow(new byte[] { })]
    [DataRow(new byte[] { 2, 1, 2 })]
    public void TryUnwrap_OtherContextIdOrNone_IsDropped(byte[] payload)
    {
        Assert.IsFalse(MasqueDatagramPayload.TryUnwrap(payload, out var content));
        Assert.IsTrue(content.IsEmpty);
    }
}
