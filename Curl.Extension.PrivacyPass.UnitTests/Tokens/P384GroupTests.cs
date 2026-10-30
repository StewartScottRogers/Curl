using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>Pins the P-384 group arithmetic against the base class library's own P-384 keys, and the SEC1 compressed encoding.</summary>
[TestClass]
public sealed class P384GroupTests
{
    private static readonly P384Point Generator = P384Group.Generator;
    private static readonly P384Point NegatedGenerator = new(Generator.X, P384Group.Prime - Generator.Y);

    [TestMethod]
    public void Multiply_PrivateKeyOfABclKey_GivesItsPublicKey()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        var parameters = key.ExportParameters(includePrivateParameters: true);

        var product = P384Group.Multiply(new BigInteger(parameters.D, isUnsigned: true, isBigEndian: true), Generator);

        Assert.AreEqual(new BigInteger(parameters.Q.X, isUnsigned: true, isBigEndian: true), product.X);
        Assert.AreEqual(new BigInteger(parameters.Q.Y, isUnsigned: true, isBigEndian: true), product.Y);
    }

    [TestMethod]
    public void Multiply_OrderOrMinusOne_GivesTheIdentityOrTheNegatedGenerator()
    {
        Assert.AreEqual(P384Point.Identity, P384Group.Multiply(P384Group.Order, Generator));
        Assert.AreEqual(NegatedGenerator, P384Group.Multiply(-1, Generator));
    }

    [TestMethod]
    public void Add_EqualOppositeAndIdentity_DoublesCancelsOrKeeps()
    {
        Assert.AreEqual(P384Group.Multiply(2, Generator), P384Group.Add(Generator, Generator));
        Assert.AreEqual(P384Point.Identity, P384Group.Add(Generator, NegatedGenerator));
        Assert.AreEqual(Generator, P384Group.Add(P384Point.Identity, Generator));
        Assert.AreEqual(Generator, P384Group.Add(Generator, P384Point.Identity));
        Assert.AreEqual(P384Group.Multiply(5, Generator), P384Group.Add(P384Group.Multiply(2, Generator), P384Group.Multiply(3, Generator)));
    }

    [TestMethod]
    public void EncodeDecode_BothParities_RoundTrip()
    {
        var encoded = P384Group.Encode(Generator);
        var negated = P384Group.Encode(NegatedGenerator);

        Assert.HasCount(P384Group.ElementLength, encoded);
        Assert.AreNotEqual(encoded[0], negated[0]);
        CollectionAssert.AreEqual(encoded[1..], negated[1..]);
        Assert.AreEqual(Generator, P384Group.Decode(encoded));
        Assert.AreEqual(NegatedGenerator, P384Group.Decode(negated));
        CollectionAssert.AreEqual(VoprfTestIssuer.EncodedPublicKey, P384Group.Encode(VoprfTestIssuer.PublicKey));
    }

    [TestMethod]
    public void Encode_Identity_Throws() =>
        Assert.ThrowsExactly<CryptographicException>(() => P384Group.Encode(P384Point.Identity));

    [TestMethod]
    public void Decode_NotACompressedPoint_IsNull()
    {
        var encoded = P384Group.Encode(Generator);
        var x = BigInteger.One;
        while (P384Group.SquareRoot(P384Group.CurveRightHandSide(x)) is not null)
        {
            x++;
        }

        Assert.IsNull(P384Group.Decode(encoded[..^1]));
        Assert.IsNull(P384Group.Decode([0x04, .. encoded[1..]]));
        Assert.IsNull(P384Group.Decode([0x02, .. Enumerable.Repeat((byte)0xFF, P384Group.ScalarLength)]));
        Assert.IsNull(P384Group.Decode([0x02, .. P384Group.EncodeScalar(x)]));
    }

    [TestMethod]
    public void DecodeScalar_ShortOrNotBelowTheOrder_IsNull()
    {
        Assert.AreEqual(P384Group.Order - 1, P384Group.DecodeScalar(P384Group.EncodeScalar(P384Group.Order - 1)));
        Assert.IsNull(P384Group.DecodeScalar(P384Group.EncodeScalar(P384Group.Order)));
        Assert.IsNull(P384Group.DecodeScalar(new byte[P384Group.ScalarLength - 1]));
    }
}
