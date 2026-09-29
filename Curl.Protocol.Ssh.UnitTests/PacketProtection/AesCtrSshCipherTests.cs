namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class AesCtrSshCipherTests
{
    private const string InitialCounter = "F0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF";

    private const string Plaintext =
        "6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710";

    [TestMethod]
    [DataRow("2B7E151628AED2A6ABF7158809CF4F3C", "874D6191B620E3261BEF6864990DB6CE9806F66B7970FDFF8617187BB9FFFDFF5AE4DF3EDBD5D35E5B4F09020DB03EAB1E031DDA2FBE03D1792170A0F3009CEE", DisplayName = "SP 800-38A F.5.1, aes128-ctr")]
    [DataRow("8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B", "1ABC932417521CA24F2B0459FE7E6E0B090339EC0AA6FAEFD5CCC2C6F4CE8E941E36B26BD1EBC670D1BD1D665620ABF74F78A7F6D29809585A97DAEC58C6B050", DisplayName = "SP 800-38A F.5.3, aes192-ctr")]
    [DataRow("603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4", "601EC313775789A5B7A7F504BBF3D228F443E3CA4D62B59ACA84E990CACAF5C52B0930DAA23DE94CE87017BA2D84988DDFC9C58DB67AADA613C2DD08457941A6", DisplayName = "SP 800-38A F.5.5, aes256-ctr")]
    public void Transform_NistVector_EncryptsBlockByBlockAcrossCalls(string key, string ciphertext)
    {
        using AesCtrSshCipher cipher = new(Convert.FromHexString(key), Convert.FromHexString(InitialCounter));
        byte[] plaintext = Convert.FromHexString(Plaintext);
        byte[] output = new byte[plaintext.Length];

        cipher.Transform(plaintext.AsSpan(0, 16), output.AsSpan(0, 16));
        cipher.Transform(plaintext.AsSpan(16), output.AsSpan(16));

        Assert.AreEqual(ciphertext, Convert.ToHexString(output));
        Assert.AreEqual(16, cipher.BlockSize);
    }

    [TestMethod]
    public void Transform_Ciphertext_DecryptsInPlaceWithAFreshCipher()
    {
        byte[] key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3C");
        byte[] counter = Convert.FromHexString(InitialCounter);
        using AesCtrSshCipher encryptor = new(key, counter);
        using AesCtrSshCipher decryptor = new(key, counter);
        byte[] bytes = Convert.FromHexString(Plaintext);

        encryptor.Transform(bytes, bytes);
        decryptor.Transform(bytes, bytes);

        Assert.AreEqual(Plaintext, Convert.ToHexString(bytes));
    }
}
