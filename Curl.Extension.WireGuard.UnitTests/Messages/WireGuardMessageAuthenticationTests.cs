using Curl.Cryptography;
using Curl.Extension.WireGuard.Messages;

namespace Curl.Extension.WireGuard.UnitTests.Messages;

[TestClass]
public sealed class WireGuardMessageAuthenticationTests
{
    private static readonly byte[] PublicKey = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    [TestMethod]
    public void DeriveMac1KeyAndCookieKey_PublicKey_HashTheLabelThenTheKey()
    {
        var mac1Key = new byte[32];
        var cookieKey = new byte[32];
        var expectedMac1Key = new byte[32];
        var expectedCookieKey = new byte[32];
        Blake2s.HashData([.. "mac1----"u8.ToArray(), .. PublicKey], expectedMac1Key);
        Blake2s.HashData([.. "cookie--"u8.ToArray(), .. PublicKey], expectedCookieKey);

        WireGuardMessageAuthentication.DeriveMac1Key(PublicKey, mac1Key);
        WireGuardMessageAuthentication.DeriveCookieKey(PublicKey, cookieKey);

        CollectionAssert.AreEqual(expectedMac1Key, mac1Key);
        CollectionAssert.AreEqual(expectedCookieKey, cookieKey);
    }

    [TestMethod]
    public void WriteMac1AndMac2_Response_AreKeyedBlake2sOverTheBytesBeforeEach()
    {
        var message = new byte[HandshakeResponseMessage.Size];
        message[0] = 2;
        var mac1Key = new byte[32];
        WireGuardMessageAuthentication.DeriveMac1Key(PublicKey, mac1Key);
        var cookie = new byte[16];
        cookie[0] = 9;
        var expectedMac1 = new byte[16];
        var expectedMac2 = new byte[16];

        WireGuardMessageAuthentication.WriteMac1(message, mac1Key);
        WireGuardMessageAuthentication.WriteMac2(message, cookie);

        Blake2s.HashData(mac1Key, message.AsSpan(0, 60), expectedMac1);
        Blake2s.HashData(cookie, message.AsSpan(0, 76), expectedMac2);
        CollectionAssert.AreEqual(expectedMac1, message[60..76]);
        CollectionAssert.AreEqual(expectedMac2, message[76..92]);
        Assert.IsTrue(WireGuardMessageAuthentication.HasValidMac1(message, mac1Key));
        Assert.IsTrue(WireGuardMessageAuthentication.HasValidMac2(message, cookie));
    }

    [TestMethod]
    public void HasValidMac1AndMac2_ByteBeforeThemChanged_AreFalse()
    {
        var message = new byte[HandshakeInitiationMessage.Size];
        var mac1Key = new byte[32];
        var cookie = new byte[16];
        WireGuardMessageAuthentication.WriteMac1(message, mac1Key);
        WireGuardMessageAuthentication.WriteMac2(message, cookie);

        message[10] ^= 1;

        Assert.IsFalse(WireGuardMessageAuthentication.HasValidMac1(message, mac1Key));
        Assert.IsFalse(WireGuardMessageAuthentication.HasValidMac2(message, cookie));
    }
}
