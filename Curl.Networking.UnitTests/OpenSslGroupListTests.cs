using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="OpenSslGroupList" /> reads <c>--curves</c>, each row the
/// <c>supported_groups</c> and <c>key_share</c> lists Ubuntu's curl 8.18.0 with OpenSSL 3.5.5
/// sent for the value (captured 2026-09-30, BL-709), or its exit 59 refusal.
/// </summary>
[TestClass]
public sealed class OpenSslGroupListTests
{
    [TestMethod]
    [DataRow("X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow("x25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow("P-384:X25519", new ushort[] { 0x0018, 0x001d }, new ushort[] { 0x0018 })]
    [DataRow("X25519MLKEM768", new ushort[] { 0x11ec }, new ushort[] { 0x11ec })]
    [DataRow("*P-256:X25519", new ushort[] { 0x0017, 0x001d }, new ushort[] { 0x0017 })]
    [DataRow("P-256:*X25519", new ushort[] { 0x0017, 0x001d }, new ushort[] { 0x001d })]
    [DataRow("X25519/P-256", new ushort[] { 0x001d, 0x0017 }, new ushort[] { 0x001d })]
    [DataRow("?bogus:X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow("P-256:X25519:P-384", new ushort[] { 0x0017, 0x001d, 0x0018 }, new ushort[] { 0x0017 })]
    [DataRow("*P-256:*X25519:P-384", new ushort[] { 0x0017, 0x001d, 0x0018 }, new ushort[] { 0x0017, 0x001d })]
    [DataRow("*X25519/*P-256:P-384", new ushort[] { 0x001d, 0x0017, 0x0018 }, new ushort[] { 0x001d, 0x0017 })]
    [DataRow("ffdhe2048:X25519", new ushort[] { 0x0100, 0x001d }, new ushort[] { 0x0100 })]
    [DataRow("X25519:X25519", new ushort[] { 0x001d }, new ushort[] { 0x001d })]
    [DataRow("X25519:P-256:-X25519", new ushort[] { 0x0017 }, new ushort[] { 0x0017 })]
    [DataRow("brainpoolP256r1:X25519", new ushort[] { 0x001a, 0x001d }, new ushort[] { 0x001d })]
    [DataRow("secp256r1:prime256v1:secp384r1:P-521:X448", new ushort[] { 0x0017, 0x0018, 0x0019, 0x001e }, new ushort[] { 0x0017 })]
    [DataRow("SecP256r1MLKEM768", new ushort[] { 0x11eb }, new ushort[] { 0x11eb })]
    [DataRow("SecP384r1MLKEM1024", new ushort[] { 0x11ed }, new ushort[] { 0x11ed })]
    [DataRow("MLKEM512", new ushort[] { 0x0200 }, new ushort[] { 0x0200 })]
    [DataRow("MLKEM768", new ushort[] { 0x0201 }, new ushort[] { 0x0201 })]
    [DataRow("MLKEM1024", new ushort[] { 0x0202 }, new ushort[] { 0x0202 })]
    [DataRow("brainpoolP256r1tls13", new ushort[] { 0x001f }, new ushort[] { 0x001f })]
    [DataRow("brainpoolP384r1tls13", new ushort[] { 0x0020 }, new ushort[] { 0x0020 })]
    [DataRow("brainpoolP512r1tls13", new ushort[] { 0x0021 }, new ushort[] { 0x0021 })]
    [DataRow("X25519:MLKEM768", new ushort[] { 0x001d, 0x0201 }, new ushort[] { 0x001d })]
    [DataRow("MLKEM768:P-256", new ushort[] { 0x0201, 0x0017 }, new ushort[] { 0x0201 })]
    [DataRow("MLKEM1024:brainpoolP512r1tls13:*SecP256r1MLKEM768", new ushort[] { 0x0202, 0x0021, 0x11eb }, new ushort[] { 0x11eb })]
    [DataRow("*SecP384r1MLKEM1024:P-384", new ushort[] { 0x11ed, 0x0018 }, new ushort[] { 0x11ed })]
    [DataRow("SecP256r1MLKEM768:X25519MLKEM768:SecP384r1MLKEM1024:*MLKEM512", new ushort[] { 0x11eb, 0x11ec, 0x11ed, 0x0200 }, new ushort[] { 0x0200 })]
    [DataRow("brainpoolP256r1:brainpoolP256r1tls13", new ushort[] { 0x001a, 0x001f }, new ushort[] { 0x001f })]
    public void Parse_WithAnAcceptedValue_OffersTheMeasuredGroupsAndKeyShares(string value, ushort[] groups, ushort[] keyShares)
    {
        var offered = OpenSslGroupList.Parse(value, ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(groups, offered.Groups.ToArray());
        CollectionAssert.AreEqual(keyShares, offered.KeyShares.ToArray());
    }

    [TestMethod]
    [DataRow("DEFAULT")]
    [DataRow("default")]
    public void Parse_WithDefault_OffersTheProfilesGroupsAndKeyShares(string value)
    {
        var offered = OpenSslGroupList.Parse(value, ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(new ushort[] { 0x11ec, 0x001d, 0x0017, 0x001e, 0x0018, 0x0019, 0x0100, 0x0101 }, offered.Groups.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x11ec, 0x001d }, offered.KeyShares.ToArray());
    }

    [TestMethod]
    public void Parse_WithDefaultLessOneGroup_OffersTheRestAndTheRemainingKeyShare()
    {
        var offered = OpenSslGroupList.Parse("DEFAULT:-X25519MLKEM768", ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(new ushort[] { 0x001d, 0x0017, 0x001e, 0x0018, 0x0019, 0x0100, 0x0101 }, offered.Groups.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x001d }, offered.KeyShares.ToArray());
    }

    [TestMethod]
    public void Parse_WithDefaultInTheSchannelBuild_OffersTheSchannelProfilesGroups()
    {
        var offered = OpenSslGroupList.Parse("DEFAULT", ClientHelloProfile.Schannel);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(ClientHelloProfile.Schannel.SupportedGroups.ToArray(), offered.Groups.ToArray());
        CollectionAssert.AreEqual(ClientHelloProfile.Schannel.KeyShareGroups.ToArray(), offered.KeyShares.ToArray());
    }

    // Accepted by OpenSSL, but nothing is left to offer: exit 35 "no suitable groups" (measured).
    [TestMethod]
    [DataRow("?bogus")]
    [DataRow("-X25519")]
    [DataRow("MLKEM768:-MLKEM768")]
    public void Parse_WithNothingTheClientCanOffer_OffersNoGroups(string value)
    {
        var offered = OpenSslGroupList.Parse(value, ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        Assert.IsEmpty(offered.Groups);
        Assert.IsEmpty(offered.KeyShares);
    }

    // Stars on TLS 1.2-only groups alone leave no key share, which the provider refuses as
    // OpenSSL's "no suitable key share" when it offers TLS 1.3 (measured 2026-10-01, BL-1082).
    [TestMethod]
    public void Parse_WithOnlyATls12OnlyGroupStarred_LeavesNoKeyShare()
    {
        var offered = OpenSslGroupList.Parse("*brainpoolP256r1:P-384", ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(new ushort[] { TlsNamedGroup.BrainpoolP256r1, TlsNamedGroup.Secp384r1 }, offered.Groups.ToArray());
        Assert.IsEmpty(offered.KeyShares);
    }

    // Every name OpenSSL 3.5 knows is a group the client can offer (BL-1049): none is dropped.
    [TestMethod]
    [DataRow("SecP256r1MLKEM768", TlsNamedGroup.SecP256r1MlKem768)]
    [DataRow("SecP384r1MLKEM1024", TlsNamedGroup.SecP384r1MlKem1024)]
    [DataRow("MLKEM512", TlsNamedGroup.MlKem512)]
    [DataRow("MLKEM768", TlsNamedGroup.MlKem768)]
    [DataRow("MLKEM1024", TlsNamedGroup.MlKem1024)]
    [DataRow("brainpoolP256r1tls13", TlsNamedGroup.BrainpoolP256r1Tls13)]
    [DataRow("brainpoolP384r1tls13", TlsNamedGroup.BrainpoolP384r1Tls13)]
    [DataRow("brainpoolP512r1tls13", TlsNamedGroup.BrainpoolP512r1Tls13)]
    public void Parse_WithAnMlKemOrBrainpoolTls13Group_OffersItRatherThanDroppingIt(string value, int group)
    {
        var offered = OpenSslGroupList.Parse($"{value}:X25519", ClientHelloProfile.OpenSsl);

        Assert.IsNotNull(offered);
        CollectionAssert.AreEqual(new ushort[] { (ushort)group, TlsNamedGroup.X25519 }, offered.Groups.ToArray());
        CollectionAssert.AreEqual(new ushort[] { (ushort)group }, offered.KeyShares.ToArray());
    }

    // Measured exit 59: curl: (59) failed setting curves list: '<value>'.
    [TestMethod]
    [DataRow("bogus")]
    [DataRow("X25519:bogus")]
    [DataRow("X25519:")]
    [DataRow(":X25519")]
    [DataRow("X25519::P-256")]
    [DataRow("x25519,P-256")]
    [DataRow("*DEFAULT")]
    [DataRow("-bogus")]
    [DataRow("")]
    public void Parse_WithARefusedValue_ReturnsNull(string value) =>
        Assert.IsNull(OpenSslGroupList.Parse(value, ClientHelloProfile.OpenSsl));

    [TestMethod]
    public void Parse_WithNullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => OpenSslGroupList.Parse(null!, ClientHelloProfile.OpenSsl));
        Assert.ThrowsExactly<ArgumentNullException>(() => OpenSslGroupList.Parse("X25519", null!));
    }
}
