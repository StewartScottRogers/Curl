using System.Numerics;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="BrainpoolEcdh" /> to RFC 7027 appendix A (brainpoolP256r1, P384r1 and
/// P512r1 key exchanges: both public keys and the shared x-coordinate) and checks that a
/// peer point of the wrong form, off the curve or at infinity is refused.
/// </summary>
[TestClass]
public sealed class BrainpoolEcdhTests
{
    // RFC 7027 A.1, A.2 and A.3: dA, x_qA, y_qA, dB, x_qB, y_qB, x_Z.
    private static readonly string[][] Rfc7027Vectors =
    [
        [
            "81DB1EE100150FF2EA338D708271BE38300CB54241D79950F77B063039804F1D",
            "44106E913F92BC02A1705D9953A8414DB95E1AAA49E81D9E85F929A8E3100BE5",
            "8AB4846F11CACCB73CE49CBDD120F5A900A69FD32C272223F789EF10EB089BDC",
            "55E40BC41E37E3E2AD25C3C6654511FFA8474A91A0032087593852D3E7D76BD3",
            "8D2D688C6CF93E1160AD04CC4429117DC2C41825E1E9FCA0ADDD34E6F1B39F7B",
            "990C57520812BE512641E47034832106BC7D3E8DD0E4C7F1136D7006547CEC6A",
            "89AFC39D41D3B327814B80940B042590F96556EC91E6AE7939BCE31F3A18BF2B",
        ],
        [
            "1E20F5E048A5886F1F157C74E91BDE2B98C8B52D58E5003D57053FC4B0BD65D6F15EB5D1EE1610DF870795143627D042",
            "68B665DD91C195800650CDD363C625F4E742E8134667B767B1B476793588F885AB698C852D4A6E77A252D6380FCAF068",
            "55BC91A39C9EC01DEE36017B7D673A931236D2F1F5C83942D049E3FA20607493E0D038FF2FD30C2AB67D15C85F7FAA59",
            "032640BC6003C59260F7250C3DB58CE647F98E1260ACCE4ACDA3DD869F74E01F8BA5E0324309DB6A9831497ABAC96670",
            "4D44326F269A597A5B58BBA565DA5556ED7FD9A8A9EB76C25F46DB69D19DC8CE6AD18E404B15738B2086DF37E71D1EB4",
            "62D692136DE56CBE93BF5FA3188EF58BC8A3A0EC6C1E151A21038A42E9185329B5B275903D192F8D4E1F32FE9CC78C48",
            "0BD9D3A7EA0B3D519D09D8E48D0785FB744A6B355E6304BC51C229FBBCE239BBADF6403715C35D4FB2A5444F575D4F42",
        ],
        [
            "16302FF0DBBB5A8D733DAB7141C1B45ACBC8715939677F6A56850A38BD87BD59B09E80279609FF333EB9D4C061231FB26F92EEB04982A5F1D1764CAD57665422",
            "0A420517E406AAC0ACDCE90FCD71487718D3B953EFD7FBEC5F7F27E28C6149999397E91E029E06457DB2D3E640668B392C2A7E737A7F0BF04436D11640FD09FD",
            "72E6882E8DB28AAD36237CD25D580DB23783961C8DC52DFA2EC138AD472A0FCEF3887CF62B623B2A87DE5C588301EA3E5FC269B373B60724F5E82A6AD147FDE7",
            "230E18E1BCC88A362FA54E4EA3902009292F7F8033624FD471B5D8ACE49D12CFABBC19963DAB8E2F1EBA00BFFB29E4D72D13F2224562F405CB80503666B25429",
            "9D45F66DE5D67E2E6DB6E93A59CE0BB48106097FF78A081DE781CDB31FCE8CCBAAEA8DD4320C4119F1E9CD437A2EAB3731FA9668AB268D871DEDA55A5473199F",
            "2FDC313095BCDD5FB3A91636F07A959C8E86B5636A1E930E8396049CB481961D365CC11453A06C719835475B12CB52FC3C383BCE35E27EF194512B71876285FA",
            "A7927098655F1F9976FA50A9D566865DC530331846381C87256BAF3226244B76D36403C024D7BBF0AA0803EAFF405D3D24F11A9B5C0BEF679FE1454B21C4CD1F",
        ],
    ];

    // RFC 5639 section 3: the field primes p and the group orders q.
    private static readonly string[] Primes =
    [
        "A9FB57DBA1EEA9BC3E660A909D838D726E3BF623D52620282013481D1F6E5377",
        "8CB91E82A3386D280F5D6F7E50E641DF152F7109ED5456B412B1DA197FB71123ACD3A729901D1A71874700133107EC53",
        "AADD9DB8DBE9C48B3FD4E6AE33C9FC07CB308DB3B3C9D20ED6639CCA703308717D4D9B009BC66842AECDA12AE6A380E62881FF2F2D82C68528AA6056583A48F3",
    ];

    private static readonly string[] Orders =
    [
        "A9FB57DBA1EEA9BC3E660A909D838D718C397AA3B561A6F7901E0E82974856A7",
        "8CB91E82A3386D280F5D6F7E50E641DF152F7109ED5456B31F166E6CAC0425A7CF3AB6AF6B7FC3103B883202E9046565",
        "AADD9DB8DBE9C48B3FD4E6AE33C9FC07CB308DB3B3C9D20ED6639CCA70330870553E5C414CA92619418661197FAC10471DB1D381085DDADDB58796829CA90069",
    ];

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void ComputePublicKey_Rfc7027AppendixA_GivesBothPublicKeys(BrainpoolCurve curve)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[(int)curve];
        diagnostics.Arrange("vector source", $"RFC 7027 Appendix A.{(int)curve + 1}, {curve}");
        diagnostics.Bytes("dA", Convert.FromHexString(vector[0]));
        diagnostics.Bytes("dB", Convert.FromHexString(vector[3]));

        byte[] publicA = PublicKey(curve, vector[0]);
        byte[] publicB = PublicKey(curve, vector[3]);
        diagnostics.Bytes("qA", publicA);
        diagnostics.Bytes("qB", publicB);
        diagnostics.Act("public key lengths", $"{publicA.Length}, {publicB.Length}");

        diagnostics.Diff("qA", Convert.FromHexString("04" + vector[1] + vector[2]), publicA);
        diagnostics.Diff("qB", Convert.FromHexString("04" + vector[4] + vector[5]), publicB);
        Assert.AreEqual("04" + vector[1] + vector[2], Hex(publicA));
        Assert.AreEqual("04" + vector[4] + vector[5], Hex(publicB));
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void TryComputeSharedSecret_Rfc7027AppendixA_GivesXzFromEitherSide(BrainpoolCurve curve)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[(int)curve];
        byte[] fromA = new byte[BrainpoolEcdh.GetSharedSecretLength(curve)];
        byte[] fromB = new byte[BrainpoolEcdh.GetSharedSecretLength(curve)];
        diagnostics.Arrange("vector source", $"RFC 7027 Appendix A.{(int)curve + 1}, {curve}");
        diagnostics.Bytes("dA", Convert.FromHexString(vector[0]));
        diagnostics.Bytes("dB", Convert.FromHexString(vector[3]));
        diagnostics.Bytes("qA", Convert.FromHexString("04" + vector[1] + vector[2]));
        diagnostics.Bytes("qB", Convert.FromHexString("04" + vector[4] + vector[5]));

        bool agreedA = BrainpoolEcdh.TryComputeSharedSecret(curve, Convert.FromHexString(vector[0]), Convert.FromHexString("04" + vector[4] + vector[5]), fromA);
        bool agreedB = BrainpoolEcdh.TryComputeSharedSecret(curve, Convert.FromHexString(vector[3]), Convert.FromHexString("04" + vector[1] + vector[2]), fromB);
        diagnostics.Act("agreed (A, B)", $"{agreedA}, {agreedB}");

        diagnostics.Diff("shared secret from A", Convert.FromHexString(vector[6]), fromA);
        diagnostics.Diff("shared secret from B", Convert.FromHexString(vector[6]), fromB);
        Assert.IsTrue(agreedA);
        Assert.IsTrue(agreedB);
        Assert.AreEqual(vector[6], Hex(fromA));
        Assert.AreEqual(vector[6], Hex(fromB));
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1, 32, 65)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1, 48, 97)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1, 64, 129)]
    public void Lengths_EachCurve_AreItsFieldLengthAndUncompressedPoint(BrainpoolCurve curve, int keyLength, int publicKeyLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve", curve);

        int privateKeyLength = BrainpoolEcdh.GetPrivateKeyLength(curve);
        int sharedSecretLength = BrainpoolEcdh.GetSharedSecretLength(curve);
        int actualPublicKeyLength = BrainpoolEcdh.GetPublicKeyLength(curve);
        diagnostics.Act("private key, shared secret, public key lengths", $"{privateKeyLength}, {sharedSecretLength}, {actualPublicKeyLength}");

        diagnostics.Assert("private key, shared secret, public key lengths", $"{keyLength}, {keyLength}, {publicKeyLength}", $"{privateKeyLength}, {sharedSecretLength}, {actualPublicKeyLength}");
        Assert.AreEqual(keyLength, privateKeyLength);
        Assert.AreEqual(keyLength, sharedSecretLength);
        Assert.AreEqual(publicKeyLength, actualPublicKeyLength);
    }

    [TestMethod]
    public void GetPrivateKeyLength_UnknownCurve_ThrowsArgumentOutOfRange()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve", 3);

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BrainpoolEcdh.GetPrivateKeyLength((BrainpoolCurve)3));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void GeneratePrivateKey_TwoParties_AgreeOnTheSecret(BrainpoolCurve curve)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve", curve);
        byte[] alice = new byte[BrainpoolEcdh.GetPrivateKeyLength(curve)];
        byte[] bob = new byte[alice.Length];
        BrainpoolEcdh.GeneratePrivateKey(curve, alice);
        BrainpoolEcdh.GeneratePrivateKey(curve, bob);
        byte[] alicePublic = new byte[BrainpoolEcdh.GetPublicKeyLength(curve)];
        byte[] bobPublic = new byte[alicePublic.Length];
        BrainpoolEcdh.ComputePublicKey(curve, alice, alicePublic);
        BrainpoolEcdh.ComputePublicKey(curve, bob, bobPublic);
        byte[] aliceSecret = new byte[alice.Length];
        byte[] bobSecret = new byte[alice.Length];
        diagnostics.Arrange("private key length", alice.Length);
        diagnostics.Bytes("alice public key", alicePublic);
        diagnostics.Bytes("bob public key", bobPublic);

        bool aliceAgreed = BrainpoolEcdh.TryComputeSharedSecret(curve, alice, bobPublic, aliceSecret);
        bool bobAgreed = BrainpoolEcdh.TryComputeSharedSecret(curve, bob, alicePublic, bobSecret);
        diagnostics.Act("agreed (alice, bob)", $"{aliceAgreed}, {bobAgreed}");

        diagnostics.Diff("shared secret", aliceSecret, bobSecret);
        diagnostics.Assert("private keys differ", true, !alice.AsSpan().SequenceEqual(bob));
        Assert.IsTrue(aliceAgreed);
        Assert.IsTrue(bobAgreed);
        CollectionAssert.AreEqual(aliceSecret, bobSecret);
        CollectionAssert.AreNotEqual(alice, bob);
    }

    [TestMethod]
    public void GeneratePrivateKey_WrongLength_ThrowsArgument()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve and destination length", "BrainpoolP256r1, 31");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => BrainpoolEcdh.GeneratePrivateKey(BrainpoolCurve.BrainpoolP256r1, new byte[31]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void DerivePrivateKey_RandomThatReducesToZero_GivesOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(BrainpoolCurve.BrainpoolP256r1);
        byte[] random = new byte[40];
        Convert.FromHexString(Orders[0]).CopyTo(random, 8);
        byte[] fromOrder = new byte[32];
        byte[] fromZero = new byte[32];
        diagnostics.Arrange("curve order source", "RFC 5639 section 3.4, brainpoolP256r1 q");
        diagnostics.Bytes("random equal to q", random);

        BrainpoolEcdh.DerivePrivateKey(domain, random, fromOrder);
        BrainpoolEcdh.DerivePrivateKey(domain, new byte[40], fromZero);
        diagnostics.Act("from q", Hex(fromOrder));
        diagnostics.Act("from zero", Hex(fromZero));

        diagnostics.Diff("from q", Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000001"), fromOrder);
        diagnostics.Diff("from zero", Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000001"), fromZero);
        Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000001", Hex(fromOrder));
        Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000001", Hex(fromZero));
    }

    [TestMethod]
    public void DerivePrivateKey_RandomAboveTheOrder_GivesItsResidue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BrainpoolDomainParameters domain = BrainpoolDomainParameters.For(BrainpoolCurve.BrainpoolP256r1);
        byte[] random = new byte[40];
        Convert.FromHexString(Orders[0]).CopyTo(random, 8);
        random[^1] += 5;
        byte[] privateKey = new byte[32];
        diagnostics.Arrange("curve order source", "RFC 5639 section 3.4, brainpoolP256r1 q");
        diagnostics.Bytes("random equal to q + 5", random);

        BrainpoolEcdh.DerivePrivateKey(domain, random, privateKey);
        diagnostics.Act("private key", Hex(privateKey));

        diagnostics.Diff("private key", Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000005"), privateKey);
        Assert.AreEqual("0000000000000000000000000000000000000000000000000000000000000005", Hex(privateKey));
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void TryComputeSharedSecret_InvalidPeerPoints_ReturnsFalseAndZeroesTheSecret(BrainpoolCurve curve)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[(int)curve];
        byte[] valid = Convert.FromHexString("04" + vector[4] + vector[5]);
        int length = BrainpoolEcdh.GetPrivateKeyLength(curve);
        byte[] compressed = valid[..(1 + length)];
        compressed[0] = 0x02;
        byte[] wrongPrefix = (byte[])valid.Clone();
        wrongPrefix[0] = 0x06;
        byte[] offCurve = (byte[])valid.Clone();
        offCurve[^1] ^= 1;
        byte[] xNotBelowPrime = Convert.FromHexString("04" + Primes[(int)curve] + vector[5]);
        byte[] yNotBelowPrime = Convert.FromHexString("04" + vector[4] + Primes[(int)curve]);
        byte[] origin = new byte[valid.Length];
        origin[0] = 0x04;
        diagnostics.Arrange("vector source", $"RFC 7027 Appendix A.{(int)curve + 1} qB, altered; RFC 5639 section 3 p");
        diagnostics.Arrange("peers", "one byte, compressed, wrong prefix, off curve, x = p, y = p, origin, truncated");
        diagnostics.Bytes("dA", Convert.FromHexString(vector[0]));

        foreach (byte[] peer in new[] { [0x00], compressed, wrongPrefix, offCurve, xNotBelowPrime, yNotBelowPrime, origin, valid[..^1] })
        {
            byte[] secret = new byte[length];
            Array.Fill(secret, (byte)0xFF);

            bool agreed = BrainpoolEcdh.TryComputeSharedSecret(curve, Convert.FromHexString(vector[0]), peer, secret);
            diagnostics.Bytes("peer", peer);
            diagnostics.Act("agreed", agreed);

            diagnostics.Diff("secret", new byte[length], secret);
            Assert.IsFalse(agreed, Hex(peer));
            Assert.IsTrue(secret.All(value => value == 0), Hex(peer));
        }
    }

    [TestMethod]
    [DataRow(BrainpoolCurve.BrainpoolP256r1)]
    [DataRow(BrainpoolCurve.BrainpoolP384r1)]
    [DataRow(BrainpoolCurve.BrainpoolP512r1)]
    public void TryComputeSharedSecret_PeerPointWithOnlyYRaisedByThePrime_ReturnsFalseAndZeroesTheSecret(BrainpoolCurve curve)
    {
        // x is the peer's own coordinate and y + p (or (p - y) + p, the point's negation)
        // still reduces to a point of the curve, so only the check that y is below p refuses it.
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[(int)curve];
        int length = BrainpoolEcdh.GetPrivateKeyLength(curve);
        BigInteger prime = Unsigned(Primes[(int)curve]);
        BigInteger y = Unsigned(vector[5]);
        BigInteger raised = y + prime < BigInteger.One << (8 * length) ? y + prime : prime - y + prime;
        Assert.IsTrue(raised < BigInteger.One << (8 * length));
        byte[] peer = Convert.FromHexString("04" + vector[4] + Convert.ToHexString(raised.ToByteArray(isUnsigned: true, isBigEndian: true)).PadLeft(2 * length, '0'));
        byte[] secret = new byte[length];
        Array.Fill(secret, (byte)0xFF);
        diagnostics.Arrange("vector source", $"RFC 7027 Appendix A.{(int)curve + 1} qB with y raised by RFC 5639's p");
        diagnostics.Bytes("peer", peer);

        bool agreed = BrainpoolEcdh.TryComputeSharedSecret(curve, Convert.FromHexString(vector[0]), peer, secret);
        diagnostics.Act("agreed", agreed);

        diagnostics.Diff("secret", new byte[length], secret);
        Assert.IsFalse(agreed);
        Assert.IsTrue(secret.All(value => value == 0));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("00")]
    [DataRow("A9FB57DBA1EEA9BC3E660A909D838D718C397AA3B561A6F7901E0E82974856A7")]
    [DataRow("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    public void ComputePublicKey_PrivateKeyOfWrongLengthOrOutOfRange_ThrowsArgument(string privateKey)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("private key", Convert.FromHexString(privateKey));
        diagnostics.Arrange("curve", BrainpoolCurve.BrainpoolP256r1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => BrainpoolEcdh.ComputePublicKey(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(privateKey), new byte[65]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void ComputePublicKey_DestinationOfWrongLength_ThrowsArgument()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("curve and destination length", "BrainpoolP256r1, 64");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => BrainpoolEcdh.ComputePublicKey(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(Rfc7027Vectors[0][0]), new byte[64]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void TryComputeSharedSecret_DestinationOfWrongLength_ThrowsArgument()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[0];
        diagnostics.Arrange("curve and destination length", "BrainpoolP256r1, 33");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => BrainpoolEcdh.TryComputeSharedSecret(BrainpoolCurve.BrainpoolP256r1, Convert.FromHexString(vector[0]), Convert.FromHexString("04" + vector[4] + vector[5]), new byte[33]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void TryComputeSharedSecret_PrivateKeyOutOfRange_ThrowsArgument()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] vector = Rfc7027Vectors[0];
        diagnostics.Arrange("private key", "32 zero bytes, below the range [1, q)");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => BrainpoolEcdh.TryComputeSharedSecret(BrainpoolCurve.BrainpoolP256r1, new byte[32], Convert.FromHexString("04" + vector[4] + vector[5]), new byte[32]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryComputeSharedSecret_AgainstWindowsCng_GivesTheSameSecret()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using System.Security.Cryptography.ECDiffieHellman cng = System.Security.Cryptography.ECDiffieHellman.Create(System.Security.Cryptography.ECCurve.NamedCurves.brainpoolP384r1);
        System.Security.Cryptography.ECParameters parameters = cng.ExportParameters(false);
        byte[] peer = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        byte[] privateKey = Convert.FromHexString(Rfc7027Vectors[1][0]);
        byte[] ours = new byte[48];
        using System.Security.Cryptography.ECDiffieHellman mine = System.Security.Cryptography.ECDiffieHellman.Create(new System.Security.Cryptography.ECParameters
        {
            Curve = System.Security.Cryptography.ECCurve.NamedCurves.brainpoolP384r1,
            D = privateKey,
            Q = new System.Security.Cryptography.ECPoint
            {
                X = Convert.FromHexString(Rfc7027Vectors[1][1]),
                Y = Convert.FromHexString(Rfc7027Vectors[1][2]),
            },
        });
        diagnostics.Arrange("vector source", "RFC 7027 Appendix A.2 dA against a fresh Windows CNG brainpoolP384r1 key");
        diagnostics.Bytes("peer", peer);

        bool agreed = BrainpoolEcdh.TryComputeSharedSecret(BrainpoolCurve.BrainpoolP384r1, privateKey, peer, ours);
        byte[] theirs = mine.DeriveRawSecretAgreement(cng.PublicKey);
        diagnostics.Act("agreed", agreed);

        diagnostics.Diff("shared secret", theirs, ours);
        Assert.IsTrue(agreed);
        CollectionAssert.AreEqual(theirs, ours);
    }

    private static byte[] PublicKey(BrainpoolCurve curve, string privateKey)
    {
        byte[] publicKey = new byte[BrainpoolEcdh.GetPublicKeyLength(curve)];
        BrainpoolEcdh.ComputePublicKey(curve, Convert.FromHexString(privateKey), publicKey);
        return publicKey;
    }

    private static string Hex(byte[] value) => Convert.ToHexString(value);

    private static BigInteger Unsigned(string hex) => new(Convert.FromHexString(hex), isUnsigned: true, isBigEndian: true);
}
