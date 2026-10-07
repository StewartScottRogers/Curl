using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="AeadAesCcm" /> to RFC 3610 section 8's packet vectors 1 to 24 and NIST
/// SP 800-38C appendix C's examples C.1 to C.3 in both directions, checks a changed bit is
/// the <c>false</c> of ADR-0118 with the plaintext left all zero, and checks the long
/// associated-data length encoding, the length field limit, and its argument and disposal
/// rules.
/// </summary>
[TestClass]
public sealed class AeadAesCcmTests
{
    // NIST SP 800-38C appendix C: the key of every example, and the prefixes its nonce,
    // associated data and payload are cut from.
    private const string NistKey = "404142434445464748494A4B4C4D4E4F";
    private const string NistNonce = "101112131415161718191A1B1C";
    private const string NistAssociatedData = "000102030405060708090A0B0C0D0E0F10111213";
    private const string NistPayload = "202122232425262728292A2B2C2D2E2F3031323334353637";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 3610 section 8, packet vectors 1 to 24: the key, the nonce, the number of
    // cleartext header octets (the associated data), the input packet, and the
    // authenticated and encrypted output packet (header, ciphertext, then the tag).
    [TestMethod]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000003020100A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E", "0001020304050607588C979A61C663D2F066D0C2C0F989806D5F6B61DAC38417E8D12CFDF926E0")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000004030201A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "000102030405060772C91A36E135F8CF291CA894085C87E3CC15C439C9E43A3BA091D56E10400916")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000005040302A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20", "000102030405060751B1E5F44A197D1DA46B0F8E2D282AE871E838BB64DA8596574ADAA76FBD9FB0C5")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000006050403A0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E", "000102030405060708090A0BA28C6865939A9A79FAAA5C4C2A9D4A91CDAC8C96C861B9C9E61EF1")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000007060504A0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "000102030405060708090A0BDCF1FB7B5D9E23FB9D4E131253658AD86EBDCA3E51E83F077D9C2D93")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000008070605A0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20", "000102030405060708090A0B6FC1B011F006568B5171A42D953D469B2570A4BD87405A0443AC91CB94")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "00000009080706A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E", "00010203040506070135D1B2C95F41D5D1D4FEC185D166B8094E999DFED96C048C56602C97ACBB7490")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "0000000A090807A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "00010203040506077B75399AC0831DD2F0BBD75879A2FD8F6CAE6B6CD9B7DB24C17B4433F434963F34B4")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "0000000B0A0908A0A1A2A3A4A5", 8, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20", "000102030405060782531A60CC24945A4B8279181AB5C84DF21CE7F9B73F42E197EA9C07E56B5EB17E5F4E")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "0000000C0B0A09A0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E", "000102030405060708090A0B07342594157785152B074098330ABB141B947B566AA9406B4D999988DD")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "0000000D0C0B0AA0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", "000102030405060708090A0B676BB20380B0E301E8AB79590A396DA78B834934F53AA2E9107A8B6C022C")]
    [DataRow("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF", "0000000E0D0C0BA0A1A2A3A4A5", 12, "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20", "000102030405060708090A0BC0FFA0D6F05BDB67F24D43A4338D2AA4BED7B20E43CD1AA31662E7AD65D6DB")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00412B4EA9CDBE3C9696766CFA", 8, "0BE1A88BACE018B108E8CF97D820EA258460E96AD9CF5289054D895CEAC47C", "0BE1A88BACE018B14CB97F86A2A4689A877947AB8091EF5386A6FFBDD080F8E78CF7CB0CDDD7B3")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "0033568EF7B2633C9696766CFA", 8, "63018F76DC8A1BCB9020EA6F91BDD85AFA0039BA4BAFF9BFB79C7028949CD0EC", "63018F76DC8A1BCB4CCB1E7CA981BEFAA0726C55D378061298C85C92814ABC33C52EE81D7D77C08A")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00103FE41336713C9696766CFA", 8, "AA6CFA36CAE86B40B916E0EACC1C00D7DCEC68EC0B3BBB1A02DE8A2D1AA346132E", "AA6CFA36CAE86B40B1D23A2220DDC0AC900D9AA03C61FCF4A559A4417767089708A776796EDB723506")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00764C63B8058E3C9696766CFA", 12, "D0D0735C531E1BECF049C24412DAAC5630EFA5396F770CE1A66B21F7B2101C", "D0D0735C531E1BECF049C24414D253C3967B70609B7CBB7C499160283245269A6F49975BCADEAF")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00F8B678094E3B3C9696766CFA", 12, "77B60F011C03E1525899BCAEE88B6A46C78D63E52EB8C546EFB5DE6F75E9CC0D", "77B60F011C03E1525899BCAE5545FF1A085EE2EFBF52B2E04BEE1E2336C73E3F762C0C7744FE7E3C")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00D560912D3F703C9696766CFA", 12, "CD9044D2B71FDB8120EA60C06435ACBAFB11A82E2F071D7CA4A5EBD93A803BA87F", "CD9044D2B71FDB8120EA60C0009769ECABDF48625594C59251E6035722675E04C847099E5AE0704551")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "0042FFF8F1951C3C9696766CFA", 8, "D85BC7E69F944FB88A19B950BCF71A018E5E6701C91787659809D67DBEDD18", "D85BC7E69F944FB8BC218DAA947427B6DB386A99AC1AEF23ADE0B52939CB6A637CF9BEC2408897C6BA")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "00920F40E56CDC3C9696766CFA", 8, "74A0EBC9069F5B371761433C37C5A35FC1F39F406302EB907C6163BE38C98437", "74A0EBC9069F5B375810E6FD25874022E80361A478E3E9CF484AB04F447EFFF6F0A477CC2FC9BF548944")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "0027CA0C7120BC3C9696766CFA", 8, "44A3AA3AAE6475CAA434A8E58500C6E41530538862D686EA9E81301B5AE4226BFA", "44A3AA3AAE6475CAF2BEED7BC5098E83FEB5B31608F8E29C38819A89C8E776F1544D4151A4ED3A8B87B9CE")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "005B8CCBCD9AF83C9696766CFA", 12, "EC46BB63B02520C33C49FD70B96B49E21D621741632875DB7F6C9243D2D7C2", "EC46BB63B02520C33C49FD7031D750A09DA3ED7FDDD49A2032AABF17EC8EBF7D22C8088C666BE5C197")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "003EBE94044B9A3C9696766CFA", 12, "47A65AC78B3D594227E85E71E2FCFBB880442C731BF95167C8FFD7895E337076", "47A65AC78B3D594227E85E71E882F1DBD38CE3EDA7C23F04DD65071EB41342ACDF7E00DCCEC7AE52987D")]
    [DataRow("D7828D13B2B0BDC325A76236DF93CC6B", "008D493B30AE8B3C9696766CFA", 12, "6E37A6EF546D955D34AB6059ABF21C0B02FEB88F856DF4A37381BCE3CC128517D4", "6E37A6EF546D955D34AB6059F32905B88A641B04B9C9FFB58CC390900F3DA12AB16DCE9E82EFA16DA62059")]
    public void EncryptAndTryDecrypt_Rfc3610PacketVector_MatchThePublishedPacket(string key, string nonce, int headerLength, string input, string output)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] inputBytes = Convert.FromHexString(input);
        byte[] outputBytes = Convert.FromHexString(output);
        byte[] associatedData = inputBytes[..headerLength];
        byte[] plaintext = inputBytes[headerLength..];
        byte[] expectedCiphertext = outputBytes[headerLength..inputBytes.Length];
        byte[] expectedTag = outputBytes[inputBytes.Length..];
        diagnostics.Arrange("vector source", "RFC 3610 section 8 packet vector");
        diagnostics.Arrange("header length", headerLength);

        AssertSealsAndOpens(diagnostics, key, nonce, associatedData, plaintext, expectedCiphertext, expectedTag);
    }

    // NIST SP 800-38C appendix C.1 to C.3: nonce, associated data and payload lengths,
    // then the published ciphertext and tag.
    [TestMethod]
    [DataRow(7, 8, 4, "7162015B", "4DAC255D")]
    [DataRow(8, 16, 16, "D2A1F0E051EA5F62081A7792073D593D", "1FC64FBFACCD")]
    [DataRow(12, 20, 24, "E3B201A9F5B71A7A9B1CEAECCD97E70B6176AAD9A4428AA5", "484392FBC1B09951")]
    public void EncryptAndTryDecrypt_NistSp80038CAppendixCExample_MatchThePublishedCiphertextAndTag(
        int nonceLength, int associatedDataLength, int payloadLength, string ciphertext, string tag)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "NIST SP 800-38C appendix C example");
        diagnostics.Arrange("nonce, associated data and payload lengths", $"{nonceLength}, {associatedDataLength}, {payloadLength}");

        AssertSealsAndOpens(
            diagnostics,
            NistKey,
            NistNonce[..(nonceLength * 2)],
            Convert.FromHexString(NistAssociatedData[..(associatedDataLength * 2)]),
            Convert.FromHexString(NistPayload[..(payloadLength * 2)]),
            Convert.FromHexString(ciphertext),
            Convert.FromHexString(tag));
    }

    // Associated data one byte below 0xFF00 has a two-byte length; at 0xFF00 it is marked
    // 0xFF 0xFE with a four-byte length (RFC 3610 section 2.2). Tags pinned to the BCL's
    // AesCcm on Windows, 2026-09-29, over NIST's key, 12-byte nonce and 20-byte payload,
    // with associated data bytes 00, 01, ... wrapping.
    [TestMethod]
    [DataRow(0xFEFF, "7A384B5ABC7743E25ED5704DE2A85DCF")]
    [DataRow(0xFF00, "817119215DC8084CBC223EC9D9872807")]
    public void EncryptAndTryDecrypt_AssociatedDataEitherSideOfTheLongLengthMark_MatchTheBclTag(int associatedDataLength, string tag)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] associatedData = new byte[associatedDataLength];
        for (int index = 0; index < associatedData.Length; index++)
        {
            associatedData[index] = (byte)index;
        }

        diagnostics.Arrange("vector source", "BCL AesCcm tag pinned on Windows, 2026-09-29");
        diagnostics.Arrange("associated data length", associatedDataLength);

        AssertSealsAndOpens(
            diagnostics,
            NistKey,
            NistNonce[..24],
            associatedData,
            Convert.FromHexString(NistPayload[..40]),
            Convert.FromHexString("E3B201A9F5B71A7A9B1CEAECCD97E70B6176AAD9"),
            Convert.FromHexString(tag));
    }

    // NIST's key and 12-byte nonce, one byte of associated data, no payload: the tag
    // pinned to the BCL's AesCcm on Windows, 2026-09-29.
    [TestMethod]
    public void EncryptAndTryDecrypt_EmptyPayload_MatchTheBclTag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "BCL AesCcm tag pinned on Windows, 2026-09-29");
        diagnostics.Arrange("payload length", 0);

        AssertSealsAndOpens(diagnostics, NistKey, NistNonce[..24], [0x00], [], [], Convert.FromHexString("483C9B51B98D7E989063DE09E88F4E09"));
    }

    // An 11-byte nonce leaves a 4-byte length field, the one width where the length check
    // must not shift by 32 (AF-0035). NIST's key, its nonce cut to 11 bytes, 20 bytes of
    // associated data and the 24-byte payload: ciphertext and tag pinned to the BCL's
    // AesCcm on Windows, 2026-10-03.
    [TestMethod]
    public void EncryptAndTryDecrypt_ElevenByteNonce_MatchTheBclCiphertextAndTag()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "BCL AesCcm ciphertext and tag pinned on Windows, 2026-10-03");
        diagnostics.Arrange("nonce length", 11);

        AssertSealsAndOpens(
            diagnostics,
            NistKey,
            NistNonce[..22],
            Convert.FromHexString(NistAssociatedData),
            Convert.FromHexString(NistPayload),
            Convert.FromHexString("D6D28B1B24B85B4FFBE0998809DAB62E35428A3CCA41BDC8"),
            Convert.FromHexString("C8B439BFF187669ACF5B3A26AB35E946"));
    }

    // RFC 3610 packet vector 1 with one bit changed in the tag, the ciphertext or the
    // header: the tag fails, and the plaintext buffer is left all zero.
    [TestMethod]
    [DataRow("tag", 0)]
    [DataRow("tag", 7)]
    [DataRow("ciphertext", 0)]
    [DataRow("ciphertext", 22)]
    [DataRow("associatedData", 7)]
    public void TryDecrypt_Rfc3610VectorWithAChangedBit_ReturnsFalseAndZeroesThePlaintext(string changed, int index)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] output = Convert.FromHexString("0001020304050607588C979A61C663D2F066D0C2C0F989806D5F6B61DAC38417E8D12CFDF926E0");
        Dictionary<string, byte[]> inputs = new()
        {
            ["associatedData"] = output[..8],
            ["ciphertext"] = output[8..31],
            ["tag"] = output[31..],
        };
        inputs[changed][index] ^= 0x01;
        byte[] plaintext = new byte[23];
        Array.Fill(plaintext, (byte)0xAA);
        using AeadAesCcm aead = new(Convert.FromHexString("C0C1C2C3C4C5C6C7C8C9CACBCCCDCECF"));
        diagnostics.Arrange("vector source", "RFC 3610 section 8 packet vector 1");
        diagnostics.Arrange("changed input and bit index", $"{changed}, {index}");
        diagnostics.Bytes("changed tag", inputs["tag"]);

        bool succeeded = aead.TryDecrypt(Convert.FromHexString("00000003020100A0A1A2A3A4A5"), inputs["ciphertext"], inputs["tag"], plaintext, inputs["associatedData"]);
        diagnostics.Act("succeeded", succeeded);
        diagnostics.Bytes("plaintext after failure", plaintext);

        diagnostics.Assert("succeeded", false, succeeded);
        diagnostics.Diff("plaintext left all zero", new byte[plaintext.Length], plaintext);
        Assert.IsFalse(succeeded);
        Assert.IsTrue(plaintext.All(value => value == 0));
    }

    // A 13-byte nonce leaves a 2-byte length field: 65,535 bytes fit, 65,536 do not.
    [TestMethod]
    public void EncryptAndTryDecrypt_ThirteenByteNonce_TakesAtMost65535Bytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadAesCcm aead = new(Convert.FromHexString(NistKey));
        byte[] nonce = Convert.FromHexString(NistNonce);
        byte[] plaintext = new byte[65535];
        byte[] ciphertext = new byte[65535];
        byte[] tag = new byte[16];
        diagnostics.Arrange("limit", "13-byte nonce, 65,535 bytes fit and 65,536 do not");
        diagnostics.Bytes("nonce", nonce);

        aead.Encrypt(nonce, plaintext, ciphertext, tag);
        bool succeeded = aead.TryDecrypt(nonce, ciphertext, tag, plaintext);
        ArgumentException encrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.Encrypt(nonce, new byte[65536], new byte[65536], tag));
        ArgumentException decrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.TryDecrypt(nonce, new byte[65536], tag, new byte[65536]));
        diagnostics.Act("round trip succeeded", succeeded);
        diagnostics.Act("encrypt ParamName", encrypt.ParamName);
        diagnostics.Act("decrypt ParamName", decrypt.ParamName);

        diagnostics.Assert("round trip succeeded", true, succeeded);
        diagnostics.Assert("encrypt ParamName", "source", encrypt.ParamName);
        diagnostics.Assert("decrypt ParamName", "source", decrypt.ParamName);
        Assert.IsTrue(succeeded);
        Assert.AreEqual("source", encrypt.ParamName);
        Assert.AreEqual("source", decrypt.ParamName);
    }

    [TestMethod]
    [DataRow(15)]
    [DataRow(33)]
    public void Constructor_KeyNotSixteenTwentyFourOrThirtyTwoBytes_Throws(int keyLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", keyLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new AeadAesCcm(new byte[keyLength]));
        diagnostics.Act("ParamName", exception.ParamName);

        diagnostics.Assert("ParamName", "key", exception.ParamName);
        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    [DataRow(6, 4, 4, 16, "nonce")]
    [DataRow(14, 4, 4, 16, "nonce")]
    [DataRow(12, 4, 4, 2, "tag")]
    [DataRow(12, 4, 4, 18, "tag")]
    [DataRow(12, 4, 4, 9, "tag")]
    [DataRow(12, 4, 5, 16, "destination")]
    public void EncryptAndTryDecrypt_WrongLength_Throws(int nonceLength, int sourceLength, int destinationLength, int tagLength, string parameterName)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using AeadAesCcm aead = new(new byte[16]);
        diagnostics.Arrange("nonce, source, destination and tag lengths", $"{nonceLength}, {sourceLength}, {destinationLength}, {tagLength}");

        ArgumentException encrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.Encrypt(new byte[nonceLength], new byte[sourceLength], new byte[destinationLength], new byte[tagLength]));
        ArgumentException decrypt = Assert.ThrowsExactly<ArgumentException>(
            () => aead.TryDecrypt(new byte[nonceLength], new byte[sourceLength], new byte[tagLength], new byte[destinationLength]));
        diagnostics.Act("encrypt ParamName", encrypt.ParamName);
        diagnostics.Act("decrypt ParamName", decrypt.ParamName);

        diagnostics.Assert("encrypt ParamName", parameterName, encrypt.ParamName);
        diagnostics.Assert("decrypt ParamName", parameterName, decrypt.ParamName);
        Assert.AreEqual(parameterName, encrypt.ParamName);
        Assert.AreEqual(parameterName, decrypt.ParamName);
    }

    [TestMethod]
    public void Dispose_LaterCallsThrow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        AeadAesCcm aead = new(new byte[32]);
        diagnostics.Arrange("state", "disposed 32-byte-key instance");

        aead.Dispose();

        var encrypt = Assert.ThrowsExactly<ObjectDisposedException>(() => aead.Encrypt(new byte[12], [], [], new byte[16]));
        var decrypt = Assert.ThrowsExactly<ObjectDisposedException>(() => aead.TryDecrypt(new byte[12], [], new byte[16], []));
        diagnostics.Act("encrypt exception", encrypt.GetType().Name);
        diagnostics.Act("decrypt exception", decrypt.GetType().Name);

        diagnostics.Assert("encrypt exception", nameof(ObjectDisposedException), encrypt.GetType().Name);
        diagnostics.Assert("decrypt exception", nameof(ObjectDisposedException), decrypt.GetType().Name);
    }

    private static void AssertSealsAndOpens(TestDiagnostics diagnostics, string key, string nonce, byte[] associatedData, byte[] plaintext, byte[] expectedCiphertext, byte[] expectedTag)
    {
        using AeadAesCcm aead = new(Convert.FromHexString(key));
        byte[] nonceBytes = Convert.FromHexString(nonce);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[expectedTag.Length];
        byte[] decrypted = new byte[plaintext.Length];
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("nonce", nonceBytes);
        diagnostics.Bytes("plaintext", plaintext);

        aead.Encrypt(nonceBytes, plaintext, ciphertext, tag, associatedData);
        bool succeeded = aead.TryDecrypt(nonceBytes, expectedCiphertext, expectedTag, decrypted, associatedData);
        diagnostics.Bytes("ciphertext", ciphertext);
        diagnostics.Bytes("tag", tag);
        diagnostics.Act("decrypt succeeded", succeeded);

        diagnostics.Diff("ciphertext", expectedCiphertext, ciphertext);
        diagnostics.Diff("tag", expectedTag, tag);
        diagnostics.Assert("decrypt succeeded", true, succeeded);
        diagnostics.Diff("decrypted plaintext", plaintext, decrypted);
        Assert.AreEqual(Convert.ToHexString(expectedCiphertext), Convert.ToHexString(ciphertext));
        Assert.AreEqual(Convert.ToHexString(expectedTag), Convert.ToHexString(tag));
        Assert.IsTrue(succeeded);
        Assert.AreEqual(Convert.ToHexString(plaintext), Convert.ToHexString(decrypted));
    }
}
