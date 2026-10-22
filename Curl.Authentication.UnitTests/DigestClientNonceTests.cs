using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestClientNonce" /> to curl 8.21.0's cnonce shapes: 32 lower-case hex digits through SSPI,
/// 12 random bytes in base64 from curl's own Digest code.
/// </summary>
[TestClass]
public sealed class DigestClientNonceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateRandom_Called_Gives12BytesInBase64()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected decoded bytes", 12);

        string clientNonce = DigestClientNonce.CreateRandom();

        byte[] decoded = Convert.FromBase64String(clientNonce);
        diagnostics.Act("client nonce", clientNonce);
        diagnostics.Bytes("decoded client nonce", decoded);
        diagnostics.Assert("decoded length", 12, decoded.Length);
        diagnostics.Assert("text length", 16, clientNonce.Length);
        Assert.HasCount(12, decoded);
        Assert.AreEqual(16, clientNonce.Length);
    }

    [TestMethod]
    public void CreateRandom_CalledTwice_GivesDifferentValues()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("calls", 2);

        string first = DigestClientNonce.CreateRandom();
        string second = DigestClientNonce.CreateRandom();

        diagnostics.Act("first", first);
        diagnostics.Act("second", second);
        diagnostics.Assert("values differ", true, first != second);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CreateRandomHex_Called_Gives32LowerCaseHexDigits()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected length", 32);

        // Measured: cnonce="81eed5b913007ab96776b8224946a866" (SSPI), "dab6bbea0a329577a0c691f97f35a087" (OpenSSL).
        string clientNonce = DigestClientNonce.CreateRandomHex();

        string other = DigestClientNonce.CreateRandomHex();
        bool allLowerHex = clientNonce.All(character => char.IsAsciiHexDigitLower(character) || char.IsAsciiDigit(character));
        diagnostics.Act("client nonce", clientNonce);
        diagnostics.Assert("length", 32, clientNonce.Length);
        diagnostics.Assert("all lower-case hex", true, allLowerHex);
        Assert.AreEqual(32, clientNonce.Length);
        Assert.IsTrue(allLowerHex, clientNonce);
        Assert.AreNotEqual(clientNonce, other);
    }

    [TestMethod]
    public void ForBuild_SspiBuild_Gives32LowerCaseHexDigits()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("matches SSPI build", true);

        // AF-0155, measured: curl 8.21.0 Schannel sent cnonce="889edd63675c2328372ba0489df3b38f".
        string clientNonce = DigestClientNonce.ForBuild(matchesSspiBuild: true)();

        bool allLowerHex = clientNonce.All(character => char.IsAsciiHexDigitLower(character) || char.IsAsciiDigit(character));
        diagnostics.Act("client nonce", clientNonce);
        diagnostics.Assert("length", 32, clientNonce.Length);
        diagnostics.Assert("all lower-case hex", true, allLowerHex);
        Assert.AreEqual(32, clientNonce.Length);
        Assert.IsTrue(allLowerHex, clientNonce);
    }

    [TestMethod]
    public void ForBuild_OwnDigestCode_Gives12BytesInBase64()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("matches SSPI build", false);

        string clientNonce = DigestClientNonce.ForBuild(matchesSspiBuild: false)();

        byte[] decoded = Convert.FromBase64String(clientNonce);
        diagnostics.Act("client nonce", clientNonce);
        diagnostics.Assert("decoded length", 12, decoded.Length);
        Assert.HasCount(12, decoded);
        Assert.AreEqual(16, clientNonce.Length);
    }

    [TestMethod]
    public void HexFrom_ForcedBytes_GivesThe16BytesAsLowerCaseHex()
    {
        // test823's CURL_ENTROPY=12345678 gives the bytes 34 33 32 31, 35 33 32 31, ... (ADR-0678, ADR-0751).
        var diagnostics = TestDiagnostics.For(TestContext);
        int asked = 0;
        byte[] Forced(int count)
        {
            asked = count;
            return Convert.FromHexString("34333231353332313633323137333231")[..count];
        }

        string clientNonce = DigestClientNonce.HexFrom(Forced)();

        diagnostics.Act("client nonce", clientNonce);
        diagnostics.Assert("bytes asked for", 16, asked);
        Assert.AreEqual(16, asked);
        Assert.AreEqual("34333231353332313633323137333231", clientNonce);
    }

    [TestMethod]
    [DataRow(true, "000102030405060708090a0b0c0d0e0f", DisplayName = "SSPI build: 16 bytes in hex")]
    [DataRow(false, "AAECAwQFBgcICQoL", DisplayName = "Own Digest code: 12 bytes in base64")]
    public void ForBuild_ForcedBytes_DrawsTheBuildsNonceFromThem(bool matchesSspiBuild, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        static byte[] Counting(int count) => [.. Enumerable.Range(0, count).Select(index => (byte)index)];

        string clientNonce = DigestClientNonce.ForBuild(matchesSspiBuild, Counting)();

        diagnostics.Assert("client nonce", expected, clientNonce);
        Assert.AreEqual(expected, clientNonce);
    }
}
