using System.Numerics;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Checks <see cref="FiniteFieldDiffieHellman" /> in every named group against
/// <see cref="BigInteger.ModPow" /> on fixed, public test exponents, checks that two
/// parties agree, and checks the peer-value range RFC 7919 section 5.1 and RFC 4253
/// section 8 require. No published known-answer vectors exist for these groups: NIST
/// CAVP's KAS FFC <c>dhEphem</c> vectors use generated FIPS 186 domain parameters, never
/// these safe primes.
/// </summary>
[TestClass]
public sealed class FiniteFieldDiffieHellmanTests
{
    private const string AliceExponent = "0123456789ABCDEFFEDCBA98765432100F1E2D3C4B5A69788796A5B4C3D2E1F0";
    private const string BobExponent = "8000000000000000000000000000000000000000000000000000000000000001";

    public static IEnumerable<object[]> NamedGroups =>
    [
        ["Group1"], ["Group2"], ["Group14"], ["Group16"], ["Group18"],
        ["Ffdhe2048"], ["Ffdhe3072"], ["Ffdhe4096"], ["Ffdhe6144"], ["Ffdhe8192"],
    ];

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DynamicData(nameof(NamedGroups))]
    public void ComputePublicValueAndSharedSecret_FixedExponents_EqualBigIntegerModPow(string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroupTests.NamedGroup(name);
        using var alice = new FiniteFieldDiffieHellman(group, Convert.FromHexString(AliceExponent));
        using var bob = new FiniteFieldDiffieHellman(group, Convert.FromHexString(BobExponent));
        byte[] alicePublic = new byte[group.PrimeLength];
        byte[] bobPublic = new byte[group.PrimeLength];
        byte[] aliceSecret = new byte[group.PrimeLength];
        byte[] bobSecret = new byte[group.PrimeLength];
        diagnostics.Arrange("vector source", "BigInteger.ModPow on fixed public exponents (no published vectors exist)");
        diagnostics.Arrange("group", name);
        diagnostics.Bytes("alice exponent", Convert.FromHexString(AliceExponent));
        diagnostics.Bytes("bob exponent", Convert.FromHexString(BobExponent));

        bool aliceAgreed;
        bool bobAgreed;
        using (diagnostics.Phase("public values and shared secrets"))
        {
            alice.ComputePublicValue(alicePublic);
            bob.ComputePublicValue(bobPublic);
            aliceAgreed = alice.TryComputeSharedSecret(bobPublic, aliceSecret);
            bobAgreed = bob.TryComputeSharedSecret(alicePublic, bobSecret);
        }

        diagnostics.Act("alice agreed", aliceAgreed);
        diagnostics.Act("bob agreed", bobAgreed);
        diagnostics.Bytes("alice public value", alicePublic);
        diagnostics.Bytes("shared secret", aliceSecret);

        diagnostics.Assert("alice agreed", true, aliceAgreed);
        diagnostics.Assert("bob agreed", true, bobAgreed);
        diagnostics.Diff("alice public value", ModPow(group, 2, AliceExponent), Convert.ToHexString(alicePublic));
        diagnostics.Diff("alice shared secret", ModPow(group, ToInteger(bobPublic), AliceExponent), Convert.ToHexString(aliceSecret));
        diagnostics.Diff("bob shared secret", aliceSecret, bobSecret);
        Assert.IsTrue(aliceAgreed);
        Assert.IsTrue(bobAgreed);
        Assert.AreEqual(ModPow(group, 2, AliceExponent), Convert.ToHexString(alicePublic));
        Assert.AreEqual(ModPow(group, ToInteger(bobPublic), AliceExponent), Convert.ToHexString(aliceSecret));
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
    }

    [TestMethod]
    [DynamicData(nameof(NamedGroups))]
    public void Generate_TwoParties_AgreeOnTheSharedSecret(string name)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroupTests.NamedGroup(name);
        using FiniteFieldDiffieHellman alice = FiniteFieldDiffieHellman.Generate(group);
        using FiniteFieldDiffieHellman bob = FiniteFieldDiffieHellman.Generate(group);
        byte[] alicePublic = new byte[group.PrimeLength];
        byte[] bobPublic = new byte[group.PrimeLength];
        byte[] aliceSecret = new byte[group.PrimeLength];
        byte[] bobSecret = new byte[group.PrimeLength];
        diagnostics.Arrange("group", name);
        diagnostics.Arrange("exponents", "generated at random");

        using (diagnostics.Phase("public values and shared secrets"))
        {
            alice.ComputePublicValue(alicePublic);
            bob.ComputePublicValue(bobPublic);
            Assert.IsTrue(alice.TryComputeSharedSecret(bobPublic, aliceSecret));
            Assert.IsTrue(bob.TryComputeSharedSecret(alicePublic, bobSecret));
        }

        diagnostics.Act("alice exponent length", alice.PrivateExponent.Length);
        diagnostics.Act("alice exponent top bit", alice.PrivateExponent[0] & 0x80);

        diagnostics.Diff("bob shared secret", aliceSecret, bobSecret);
        diagnostics.Assert("alice exponent length", FiniteFieldDiffieHellman.GeneratedExponentLength, alice.PrivateExponent.Length);
        diagnostics.Assert("alice exponent top bit", 0x80, alice.PrivateExponent[0] & 0x80);
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
        Assert.AreEqual(FiniteFieldDiffieHellman.GeneratedExponentLength, alice.PrivateExponent.Length);
        Assert.AreEqual(0x80, alice.PrivateExponent[0] & 0x80);
    }

    [TestMethod]
    public void ComputePublicValue_ExponentAsLongAsThePrime_EqualsBigIntegerModPow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group14;
        string exponent = Convert.ToHexString(group.Prime)[..^2] + "00";
        using var party = new FiniteFieldDiffieHellman(group, Convert.FromHexString(exponent));
        byte[] publicValue = new byte[group.PrimeLength];
        diagnostics.Arrange("vector source", "BigInteger.ModPow, Group14 with an exponent as long as the prime");
        diagnostics.Bytes("exponent", Convert.FromHexString(exponent));

        party.ComputePublicValue(publicValue);
        diagnostics.Act("public value length", publicValue.Length);
        diagnostics.Bytes("public value", publicValue);

        diagnostics.Diff("public value", ModPow(group, 2, exponent), Convert.ToHexString(publicValue));
        Assert.AreEqual(ModPow(group, 2, exponent), Convert.ToHexString(publicValue));
    }

    // Mersenne primes of 61, 89, 107 and 127 bits: limb counts 2 to 4, and 14 bytes, which is
    // not a whole number of limbs. The generator 3 and exponents are arbitrary public values.
    [TestMethod]
    [DataRow(61, "05", "1F")]
    [DataRow(89, "0123456789", "FEDCBA9876543210")]
    [DataRow(107, "FFFFFFFFFFFFFFFFFFFF", "03FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    [DataRow(127, "00000000000000000001", "0102030405060708090A0B0C0D0E0F")]
    public void TryComputeSharedSecret_ServerChosenGroup_EqualsBigIntegerModPow(int bits, string exponent, string peerValue)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] prime = ((BigInteger.One << bits) - 1).ToByteArray(isUnsigned: true, isBigEndian: true);
        Assert.IsTrue(FiniteFieldDiffieHellmanGroup.TryCreate(prime, [3], out FiniteFieldDiffieHellmanGroup? group));
        using var party = new FiniteFieldDiffieHellman(group, Convert.FromHexString(exponent));
        byte[] publicValue = new byte[group.PrimeLength];
        byte[] secret = new byte[group.PrimeLength];
        diagnostics.Arrange("vector source", $"BigInteger.ModPow, Mersenne prime 2^{bits} - 1 with generator 3");
        diagnostics.Bytes("prime", prime);
        diagnostics.Bytes("exponent", Convert.FromHexString(exponent));
        diagnostics.Bytes("peer value", Convert.FromHexString(peerValue));

        party.ComputePublicValue(publicValue);
        bool agreed = party.TryComputeSharedSecret(Convert.FromHexString(peerValue), secret);
        diagnostics.Act("agreed", agreed);

        diagnostics.Assert("agreed", true, agreed);
        diagnostics.Diff("public value", ModPow(group, 3, exponent), Convert.ToHexString(publicValue));
        diagnostics.Diff("shared secret", ModPow(group, ToInteger(Convert.FromHexString(peerValue)), exponent), Convert.ToHexString(secret));
        Assert.IsTrue(agreed);
        Assert.AreEqual(ModPow(group, 3, exponent), Convert.ToHexString(publicValue));
        Assert.AreEqual(ModPow(group, ToInteger(Convert.FromHexString(peerValue)), exponent), Convert.ToHexString(secret));
    }

    [TestMethod]
    public void Generate_ShortServerChosenPrime_DrawsOneByteLessThanThePrime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("prime", "0107");
        diagnostics.Arrange("generator", "02");
        Assert.IsTrue(FiniteFieldDiffieHellmanGroup.TryCreate([0x01, 0x07], [0x02], out FiniteFieldDiffieHellmanGroup? group));

        using FiniteFieldDiffieHellman party = FiniteFieldDiffieHellman.Generate(group);
        diagnostics.Act("exponent length", party.PrivateExponent.Length);

        diagnostics.Assert("exponent length", 1, party.PrivateExponent.Length);
        Assert.AreEqual(1, party.PrivateExponent.Length);
    }

    [TestMethod]
    public void TryComputeSharedSecret_PeerValuePaddedWithLeadingZeros_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group1;
        using var party = new FiniteFieldDiffieHellman(group, Convert.FromHexString(AliceExponent));
        byte[] peer = new byte[group.PrimeLength + 3];
        peer[^1] = 5;
        byte[] secret = new byte[group.PrimeLength];
        diagnostics.Arrange("group", "Group1");
        diagnostics.Bytes("peer value", peer);

        bool agreed = party.TryComputeSharedSecret(peer, secret);
        diagnostics.Act("agreed", agreed);
        Assert.IsTrue(agreed);

        diagnostics.Diff("shared secret", ModPow(group, 5, AliceExponent), Convert.ToHexString(secret));
        Assert.AreEqual(ModPow(group, 5, AliceExponent), Convert.ToHexString(secret));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "y = 0")]
    [DataRow(1, DisplayName = "y = 1")]
    [DataRow(-1, DisplayName = "y = p - 1")]
    [DataRow(-2, DisplayName = "y = p")]
    [DataRow(-3, DisplayName = "y = p + 1")]
    public void TryComputeSharedSecret_PeerValueOutOfRange_ReturnsFalseAndZeroesTheSecret(int which)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Ffdhe2048;
        BigInteger prime = new(group.Prime, isUnsigned: true, isBigEndian: true);
        BigInteger peer = which >= 0 ? which : prime - 2 - which;
        using var party = new FiniteFieldDiffieHellman(group, Convert.FromHexString(AliceExponent));
        byte[] secret = new byte[group.PrimeLength];
        Array.Fill(secret, (byte)0xAA);
        diagnostics.Arrange("vector source", "RFC 7919 section 5.1 and RFC 4253 section 8: 1 < y < p - 1");
        diagnostics.Arrange("group", "Ffdhe2048");
        diagnostics.Bytes("peer value", peer.ToByteArray(isUnsigned: true, isBigEndian: true));

        bool agreed = party.TryComputeSharedSecret(peer.ToByteArray(isUnsigned: true, isBigEndian: true), secret);
        diagnostics.Act("agreed", agreed);

        diagnostics.Assert("agreed", false, agreed);
        diagnostics.Diff("shared secret", new byte[secret.Length], secret);
        Assert.IsFalse(agreed);
        Assert.IsTrue(secret.All(value => value == 0));
    }

    [TestMethod]
    public void TryComputeSharedSecret_EmptyPeerValue_ReturnsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var party = new FiniteFieldDiffieHellman(FiniteFieldDiffieHellmanGroup.Group1, [7]);
        diagnostics.Arrange("group", "Group1");
        diagnostics.Arrange("peer value", "empty");

        bool agreed = party.TryComputeSharedSecret([], new byte[FiniteFieldDiffieHellmanGroup.Group1.PrimeLength]);
        diagnostics.Act("agreed", agreed);

        diagnostics.Assert("agreed", false, agreed);
        Assert.IsFalse(agreed);
    }

    [TestMethod]
    public void Dispose_ZeroesThePrivateExponentAndLaterCallsThrow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group1;
        var party = new FiniteFieldDiffieHellman(group, Convert.FromHexString(AliceExponent));
        byte[] buffer = new byte[group.PrimeLength];
        diagnostics.Arrange("group", "Group1");
        diagnostics.Bytes("exponent", Convert.FromHexString(AliceExponent));

        party.Dispose();
        diagnostics.Act("state", "disposed");

        diagnostics.Diff("private exponent", new byte[party.PrivateExponent.Length], party.PrivateExponent);
        Assert.IsTrue(party.PrivateExponent.ToArray().All(value => value == 0));
        Assert.ThrowsExactly<ObjectDisposedException>(() => party.ComputePublicValue(buffer));
        Assert.ThrowsExactly<ObjectDisposedException>(() => party.TryComputeSharedSecret([5], buffer));
        diagnostics.Assert("ComputePublicValue and TryComputeSharedSecret after Dispose", nameof(ObjectDisposedException), nameof(ObjectDisposedException));
    }

    [TestMethod]
    public void Constructor_ExponentEmptyOrLongerThanThePrime_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group1;
        diagnostics.Arrange("group", "Group1");
        diagnostics.Arrange("exponent lengths", $"0 and {group.PrimeLength + 1}; a null group");

        ArgumentException empty = Assert.ThrowsExactly<ArgumentException>(() => new FiniteFieldDiffieHellman(group, []));
        ArgumentException tooLong = Assert.ThrowsExactly<ArgumentException>(() => new FiniteFieldDiffieHellman(group, new byte[group.PrimeLength + 1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FiniteFieldDiffieHellman(null!, [7]));
        Assert.ThrowsExactly<ArgumentNullException>(() => FiniteFieldDiffieHellman.Generate(null!));
        diagnostics.Act("empty exponent", empty.GetType().Name);
        diagnostics.Act("long exponent", tooLong.GetType().Name);

        diagnostics.Assert("empty exponent", nameof(ArgumentException), empty.GetType().Name);
        diagnostics.Assert("long exponent", nameof(ArgumentException), tooLong.GetType().Name);
    }

    [TestMethod]
    public void ComputePublicValueAndTryComputeSharedSecret_WrongDestinationLength_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FiniteFieldDiffieHellmanGroup group = FiniteFieldDiffieHellmanGroup.Group1;
        using var party = new FiniteFieldDiffieHellman(group, [7]);
        byte[] shortBuffer = new byte[group.PrimeLength - 1];
        diagnostics.Arrange("group", "Group1");
        diagnostics.Arrange("destination length", shortBuffer.Length);

        ArgumentException publicValue = Assert.ThrowsExactly<ArgumentException>(() => party.ComputePublicValue(shortBuffer));
        ArgumentException sharedSecret = Assert.ThrowsExactly<ArgumentException>(() => party.TryComputeSharedSecret([5], shortBuffer));
        diagnostics.Act("ComputePublicValue", publicValue.GetType().Name);
        diagnostics.Act("TryComputeSharedSecret", sharedSecret.GetType().Name);

        diagnostics.Assert("ComputePublicValue", nameof(ArgumentException), publicValue.GetType().Name);
        diagnostics.Assert("TryComputeSharedSecret", nameof(ArgumentException), sharedSecret.GetType().Name);
    }

    private static BigInteger ToInteger(ReadOnlySpan<byte> bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    /// <summary>baseValue^exponent mod p by <see cref="BigInteger.ModPow" />, as hex padded to p's length.</summary>
    private static string ModPow(FiniteFieldDiffieHellmanGroup group, BigInteger baseValue, string exponentHex)
    {
        BigInteger result = BigInteger.ModPow(baseValue, ToInteger(Convert.FromHexString(exponentHex)), ToInteger(group.Prime));
        byte[] bytes = result.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] padded = new byte[group.PrimeLength];
        bytes.CopyTo(padded, padded.Length - bytes.Length);
        return Convert.ToHexString(padded);
    }
}
