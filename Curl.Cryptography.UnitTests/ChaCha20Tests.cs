using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="ChaCha20" /> to RFC 8439's published vectors (sections 2.1.1, 2.2.1,
/// 2.3.2, 2.4.2 and 2.6.2, Appendix A.1, A.2 and A.4), and the original 64-bit counter to
/// the RFC's state layout (ADR-0118).
/// </summary>
[TestClass]
public sealed class ChaCha20Tests
{
    private const string SequentialKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
    private const string ZeroKey = "0000000000000000000000000000000000000000000000000000000000000000";
    private const string OneKey = "0000000000000000000000000000000000000000000000000000000000000001";
    private const string JabberwockyKey = "1c9240a5eb55d38af333888604f6b5f0473917c1402b80099dca5cbc207075c0";
    private const string ZeroNonce = "000000000000000000000000";
    private const string TwoNonce = "000000000000000000000002";

    private const string SunscreenPlaintext =
        "4c616469657320616e642047656e746c656d656e206f662074686520636c617373206f66202739393a204966204920636f756c64206f6666657220796f75206f6e6c79206f6e652074697020666f7220746865206675747572652c2073756e73637265656e20776f756c642062652069742e";

    private const string SunscreenCiphertext =
        "6e2e359a2568f98041ba0728dd0d6981e97e7aec1d4360c20a27afccfd9fae0bf91b65c5524733ab8f593dabcd62b3571639d624e65152ab8f530c359f0861d807ca0dbf500d6a6156a38e088a22b65e52bc514d16ccf806818ce91ab77937365af90bbf74a35be6b40b8eedf2785e42874d";

    private const string IetfContributionPlaintext =
        "416e79207375626d697373696f6e20746f20746865204945544620696e74656e6465642062792074686520436f6e7472696275746f7220666f72207075626c69636174696f6e20617320616c6c206f722070617274206f6620616e204945544620496e7465726e65742d4472616674206f722052464320616e6420616e792073746174656d656e74206d6164652077697468696e2074686520636f6e74657874206f6620616e204945544620616374697669747920697320636f6e7369646572656420616e20224945544620436f6e747269627574696f6e222e20537563682073746174656d656e747320696e636c756465206f72616c2073746174656d656e747320696e20494554462073657373696f6e732c2061732077656c6c206173207772697474656e20616e6420656c656374726f6e696320636f6d6d756e69636174696f6e73206d61646520617420616e792074696d65206f7220706c6163652c207768696368206172652061646472657373656420746f";

    private const string IetfContributionCiphertext =
        "a3fbf07df3fa2fde4f376ca23e82737041605d9f4f4f57bd8cff2c1d4b7955ec2a97948bd3722915c8f3d337f7d370050e9e96d647b7c39f56e031ca5eb6250d4042e02785ececfa4b4bb5e8ead0440e20b6e8db09d881a7c6132f420e52795042bdfa7773d8a9051447b3291ce1411c680465552aa6c405b7764d5e87bea85ad00f8449ed8f72d0d662ab052691ca66424bc86d2df80ea41f43abf937d3259dc4b2d0dfb48a6c9139ddd7f76966e928e635553ba76c5c879d7b35d49eb2e62b0871cdac638939e25e8a1e0ef9d5280fa8ca328b351c3c765989cbcf3daa8b6ccc3aaf9f3979c92b3720fc88dc95ed84a1be059c6499b9fda236e7e818b04b0bc39c1e876b193bfe5569753f88128cc08aaa9b63d1a16f80ef2554d7189c411f5869ca52c5b83fa36ff216b9c1d30062bebcfd2dc5bce0911934fda79a86f6e698ced759c3ff9b6477338f3da4f9cd8514ea9982ccafb341b2384dd902f3d1ab7ac61dd29c6f21ba5b862f3730e37cfdc4fd806c22f221";

    private const string JabberwockyPlaintext =
        "2754776173206272696c6c69672c20616e642074686520736c6974687920746f7665730a446964206779726520616e642067696d626c6520696e2074686520776162653a0a416c6c206d696d737920776572652074686520626f726f676f7665732c0a416e6420746865206d6f6d65207261746873206f757467726162652e";

    private const string JabberwockyCiphertext =
        "62e6347f95ed87a45ffae7426f27a1df5fb69110044c0d73118effa95b01e5cf166d3df2d721caf9b21e5fb14c616871fd84c54f9d65b283196c7fe4f60553ebf39c6402c42234e32a356b3e764312a61a5532055716ead6962568f87d3f3f7704c6a8d1bcd1bf4d50d6154b6da731b187b58dfd728afa36757a797ac188d1";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 8439 section 2.1.1.
    [TestMethod]
    public void QuarterRound_Rfc8439Section211Words_GivesThePublishedWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint a = 0x11111111, b = 0x01020304, c = 0x9b8d6f43, d = 0x01234567;
        diagnostics.Arrange("vector source", "RFC 8439 section 2.1.1");
        diagnostics.Arrange("a, b, c, d", $"0x{a:x8}, 0x{b:x8}, 0x{c:x8}, 0x{d:x8}");

        ChaCha20.QuarterRound(ref a, ref b, ref c, ref d);
        diagnostics.Act("a, b, c, d", $"0x{a:x8}, 0x{b:x8}, 0x{c:x8}, 0x{d:x8}");

        diagnostics.Assert("a, b, c, d", "0xea2a92f4, 0xcb1cf8ce, 0x4581472e, 0x5881c4bb", $"0x{a:x8}, 0x{b:x8}, 0x{c:x8}, 0x{d:x8}");
        Assert.AreEqual(0xea2a92f4u, a);
        Assert.AreEqual(0xcb1cf8ceu, b);
        Assert.AreEqual(0x4581472eu, c);
        Assert.AreEqual(0x5881c4bbu, d);
    }

    // RFC 8439 section 2.2.1: QUARTERROUND(2, 7, 8, 13) changes only those four words.
    [TestMethod]
    public void QuarterRound_Rfc8439Section221State_ChangesOnlyTheFourNamedWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint[] state =
        [
            0x879531e0, 0xc5ecf37d, 0x516461b1, 0xc9a62f8a,
            0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0x2a5f714c,
            0x53372767, 0xb00a5631, 0x974c541a, 0x359e9963,
            0x5c971061, 0x3d631689, 0x2098d9d6, 0x91dbd320,
        ];
        uint[] expected =
        [
            0x879531e0, 0xc5ecf37d, 0xbdb886dc, 0xc9a62f8a,
            0x44c20ef3, 0x3390af7f, 0xd9fc690b, 0xcfacafd2,
            0xe46bea80, 0xb00a5631, 0x974c541a, 0x359e9963,
            0x5c971061, 0xccc07c79, 0x2098d9d6, 0x91dbd320,
        ];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.2.1");
        diagnostics.Arrange("state", FormatWords(state));
        diagnostics.Arrange("indices", "2, 7, 8, 13");

        ChaCha20.QuarterRound(state, 2, 7, 8, 13);
        diagnostics.Act("state", FormatWords(state));

        diagnostics.Diff("state", FormatWords(expected), FormatWords(state));
        CollectionAssert.AreEqual(expected, state);
    }

    // RFC 8439 section 2.3.2, then Appendix A.1 test vectors #1 to #5.
    [TestMethod]
    [DataRow(SequentialKey, "000000090000004a00000000", 1UL,
        "10f1e7e4d13b5915500fdd1fa32071c4c7d1f4c733c068030422aa9ac3d46c4ed2826446079faa0914c2d705d98b02a2b5129cd1de164eb9cbd083e8a2503c4e")]
    [DataRow(ZeroKey, ZeroNonce, 0UL,
        "76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8d84a376a43b8f41518a11cc387b669b2ee6586")]
    [DataRow(ZeroKey, ZeroNonce, 1UL,
        "9f07e7be5551387a98ba977c732d080dcb0f29a048e3656912c6533e32ee7aed29b721769ce64e43d57133b074d839d531ed1f28510afb45ace10a1f4b794d6f")]
    [DataRow(OneKey, ZeroNonce, 1UL,
        "3aeb5224ecf849929b9d828db1ced4dd832025e8018b8160b82284f3c949aa5a8eca00bbb4a73bdad192b5c42f73f2fd4e273644c8b36125a64addeb006c13a0")]
    [DataRow("00ff000000000000000000000000000000000000000000000000000000000000", ZeroNonce, 2UL,
        "72d54dfbf12ec44b362692df94137f328fea8da73990265ec1bbbea1ae9af0ca13b25aa26cb4a648cb9b9d1be65b2c0924a66c54d545ec1b7374f4872e99f096")]
    [DataRow(ZeroKey, TwoNonce, 0UL,
        "c2c64d378cd536374ae204b9ef933fcd1a8b2288b3dfa49672ab765b54ee27c78a970e0e955c14f3a88e741b97c286f75f8fc299e8148362fa198a39531bed6d")]
    public void ComputeBlock_Rfc8439Vector_GivesThePublishedBlock(string key, string nonce, ulong counter, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = new byte[ChaCha20.BlockSize];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.3.2 and Appendix A.1");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("nonce", Convert.FromHexString(nonce));
        diagnostics.Arrange("counter", counter);

        ChaCha20.ComputeBlock(Convert.FromHexString(key), Convert.FromHexString(nonce), counter, block);
        diagnostics.Bytes("block", block);
        diagnostics.Act("block", Convert.ToHexStringLower(block));

        diagnostics.Diff("block", Convert.FromHexString(expected), block);
        Assert.AreEqual(expected, Convert.ToHexStringLower(block));
    }

    // RFC 8439 section 2.6.2, then Appendix A.4 test vectors #1 to #3: the one-time
    // Poly1305 key is the first 32 bytes of block 0.
    [TestMethod]
    [DataRow("808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9f", "000000000001020304050607",
        "8ad5a08b905f81cc815040274ab29471a833b637e3fd0da508dbb8e2fdd1a646")]
    [DataRow(ZeroKey, ZeroNonce, "76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7")]
    [DataRow(OneKey, TwoNonce, "ecfa254f845f647473d3cb140da9e87606cb33066c447b87bc2666dde3fbb739")]
    [DataRow(JabberwockyKey, TwoNonce, "965e3bc6f9ec7ed9560808f4d229f94b137ff275ca9b3fcbdd59deaad23310ae")]
    public void ComputeBlock_Rfc8439Poly1305KeyGenerationVector_StartsWithThePublishedKey(string key, string nonce, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] block = new byte[ChaCha20.BlockSize];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.6.2 and Appendix A.4");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("nonce", Convert.FromHexString(nonce));
        diagnostics.Arrange("counter", 0);

        ChaCha20.ComputeBlock(Convert.FromHexString(key), Convert.FromHexString(nonce), 0, block);
        diagnostics.Act("one-time Poly1305 key", Convert.ToHexStringLower(block.AsSpan(0, Poly1305.KeySize)));

        diagnostics.Diff("one-time Poly1305 key", Convert.FromHexString(expected), block.AsSpan(0, Poly1305.KeySize));
        Assert.AreEqual(expected, Convert.ToHexStringLower(block.AsSpan(0, Poly1305.KeySize)));
    }

    // RFC 8439 section 2.4.2, then Appendix A.2 test vectors #1 to #3.
    [TestMethod]
    [DataRow(SequentialKey, "000000000000004a00000000", 1UL, SunscreenPlaintext, SunscreenCiphertext)]
    [DataRow(ZeroKey, ZeroNonce, 0UL,
        "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
        "76b8e0ada0f13d90405d6ae55386bd28bdd219b8a08ded1aa836efcc8b770dc7da41597c5157488d7724e03fb8d84a376a43b8f41518a11cc387b669b2ee6586")]
    [DataRow(OneKey, TwoNonce, 1UL, IetfContributionPlaintext, IetfContributionCiphertext)]
    [DataRow(JabberwockyKey, TwoNonce, 42UL, JabberwockyPlaintext, JabberwockyCiphertext)]
    public void ApplyKeyStream_Rfc8439Vector_EncryptsAndDecryptsThePublishedText(
        string key,
        string nonce,
        ulong initialCounter,
        string plaintext,
        string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] keyBytes = Convert.FromHexString(key);
        byte[] nonceBytes = Convert.FromHexString(nonce);
        byte[] encrypted = new byte[plaintext.Length / 2];
        byte[] decrypted = new byte[encrypted.Length];
        diagnostics.Arrange("vector source", "RFC 8439 section 2.4.2 and Appendix A.2");
        diagnostics.Bytes("key", keyBytes);
        diagnostics.Bytes("nonce", nonceBytes);
        diagnostics.Arrange("initial counter", initialCounter);
        diagnostics.Bytes("plaintext", Convert.FromHexString(plaintext));

        ChaCha20.ApplyKeyStream(keyBytes, nonceBytes, initialCounter, Convert.FromHexString(plaintext), encrypted);
        ChaCha20.ApplyKeyStream(keyBytes, nonceBytes, initialCounter, encrypted, decrypted);
        diagnostics.Bytes("encrypted", encrypted);
        diagnostics.Act("encrypted length", encrypted.Length);

        diagnostics.Diff("encrypted", Convert.FromHexString(ciphertext), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(plaintext), decrypted);

        Assert.AreEqual(ciphertext, Convert.ToHexStringLower(encrypted));
        Assert.AreEqual(plaintext, Convert.ToHexStringLower(decrypted));
    }

    [TestMethod]
    public void ApplyKeyStream_DestinationIsTheSource_EncryptsInPlace()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] buffer = Convert.FromHexString(SunscreenPlaintext);
        diagnostics.Arrange("vector source", "RFC 8439 section 2.4.2");
        diagnostics.Bytes("key", Convert.FromHexString(SequentialKey));
        diagnostics.Bytes("nonce", Convert.FromHexString("000000000000004a00000000"));
        diagnostics.Arrange("initial counter", 1);
        diagnostics.Bytes("plaintext", buffer);

        ChaCha20.ApplyKeyStream(Convert.FromHexString(SequentialKey), Convert.FromHexString("000000000000004a00000000"), 1, buffer, buffer);
        diagnostics.Bytes("buffer", buffer);
        diagnostics.Act("buffer length", buffer.Length);

        diagnostics.Diff("buffer", Convert.FromHexString(SunscreenCiphertext), buffer);
        Assert.AreEqual(SunscreenCiphertext, Convert.ToHexStringLower(buffer));
    }

    [TestMethod]
    public void ApplyKeyStream_EmptySource_WritesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] buffer = [0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5];
        diagnostics.Arrange("source length", 0);
        diagnostics.Arrange("initial counter", uint.MaxValue);
        diagnostics.Bytes("buffer before", buffer);

        ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, [], buffer.AsSpan(0, 0));
        diagnostics.Act("buffer after", Convert.ToHexString(buffer));

        diagnostics.Diff("buffer", Convert.FromHexString("A5A5A5A5A5A5A5A5"), buffer);
        Assert.AreEqual("A5A5A5A5A5A5A5A5", Convert.ToHexString(buffer));
    }

    // The original ChaCha puts a 64-bit counter in words 12 and 13 and an 8-byte nonce in
    // words 14 and 15, so it matches RFC 8439's block with the counter's high word as the
    // nonce's first four bytes.
    [TestMethod]
    public void ComputeBlock_OriginalNonce_UsesWordsTwelveAndThirteenAsA64BitCounter()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = Convert.FromHexString(SequentialKey);
        byte[] original = new byte[ChaCha20.BlockSize];
        byte[] ietf = new byte[ChaCha20.BlockSize];
        diagnostics.Bytes("key", key);
        diagnostics.Arrange("original nonce and counter", "0001020304050607, 0x0000000a0000000b");
        diagnostics.Arrange("RFC 8439 nonce and counter", "0a0000000001020304050607, 0x0000000b");

        ChaCha20.ComputeBlock(key, Convert.FromHexString("0001020304050607"), 0x0000000a_0000000bUL, original);
        ChaCha20.ComputeBlock(key, Convert.FromHexString("0a0000000001020304050607"), 0x0000000b, ietf);
        diagnostics.Bytes("original block", original);
        diagnostics.Act("original block length", original.Length);

        diagnostics.Diff("block", ietf, original);

        CollectionAssert.AreEqual(ietf, original);
    }

    [TestMethod]
    public void ApplyKeyStream_OriginalNonceCounterCrosses32Bits_CarriesIntoTheHighWord()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] key = Convert.FromHexString(SequentialKey);
        byte[] nonce = Convert.FromHexString("0001020304050607");
        byte[] keyStream = new byte[2 * ChaCha20.BlockSize];
        byte[] expectedSecondBlock = new byte[ChaCha20.BlockSize];
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("nonce", nonce);
        diagnostics.Arrange("initial counter", uint.MaxValue);

        ChaCha20.ApplyKeyStream(key, nonce, uint.MaxValue, new byte[keyStream.Length], keyStream);
        ChaCha20.ComputeBlock(key, nonce, 0x1_0000_0000UL, expectedSecondBlock);
        diagnostics.Bytes("second key stream block", keyStream.AsSpan(ChaCha20.BlockSize));
        diagnostics.Act("key stream length", keyStream.Length);

        diagnostics.Diff("second block against counter 0x100000000", expectedSecondBlock, keyStream.AsSpan(ChaCha20.BlockSize));

        CollectionAssert.AreEqual(expectedSecondBlock, keyStream[ChaCha20.BlockSize..]);
    }

    [TestMethod]
    public void ApplyKeyStream_RfcNonceLastBlockAtTheCounterLimit_Succeeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] output = new byte[ChaCha20.BlockSize];
        diagnostics.Arrange("initial counter", uint.MaxValue);
        diagnostics.Arrange("message length", output.Length);

        ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(ZeroNonce), uint.MaxValue, new byte[output.Length], output);
        bool allZero = output.All(value => value == 0);
        diagnostics.Bytes("output", output);
        diagnostics.Act("output all zero", allZero);

        diagnostics.Assert("output all zero", false, allZero);
        Assert.IsFalse(allZero);
    }

    [TestMethod]
    [DataRow(ZeroNonce, (ulong)uint.MaxValue, ChaCha20.BlockSize + 1)]
    [DataRow(ZeroNonce, 0x1_0000_0000UL, 1)]
    [DataRow("0000000000000000", ulong.MaxValue, ChaCha20.BlockSize + 1)]
    public void ApplyKeyStream_MessageRunsPastTheCounter_Throws(string nonce, ulong initialCounter, int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] source = new byte[length];
        diagnostics.Arrange("nonce", nonce);
        diagnostics.Arrange("initial counter", initialCounter);
        diagnostics.Arrange("message length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ApplyKeyStream(Convert.FromHexString(ZeroKey), Convert.FromHexString(nonce), initialCounter, source, source));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(31, ChaCha20.NonceSize, 16, 16)]
    [DataRow(ChaCha20.KeySize, 7, 16, 16)]
    [DataRow(ChaCha20.KeySize, ChaCha20.NonceSize, 16, 15)]
    public void ApplyKeyStream_WrongLength_Throws(int keyLength, int nonceLength, int sourceLength, int destinationLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", keyLength);
        diagnostics.Arrange("nonce length", nonceLength);
        diagnostics.Arrange("source and destination lengths", $"{sourceLength}, {destinationLength}");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ApplyKeyStream(new byte[keyLength], new byte[nonceLength], 0, new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void ComputeBlock_BlockIsNot64Bytes_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("block length", ChaCha20.BlockSize - 1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(
            () => ChaCha20.ComputeBlock(new byte[ChaCha20.KeySize], new byte[ChaCha20.NonceSize], 0, new byte[ChaCha20.BlockSize - 1]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    private static string FormatWords(uint[] words) => string.Join(" ", words.Select(word => word.ToString("x8", System.Globalization.CultureInfo.InvariantCulture)));
}
