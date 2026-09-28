namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Blowfish" /> to Eric Young's test vectors, published by Schneier at
/// https://www.schneier.com/wp-content/uploads/2015/12/vectors-2.txt (the ECB table, the
/// set_key table of 1- to 24-byte keys, and the chaining mode test), and checks its
/// argument and disposal rules (ADR-0118).
/// </summary>
[TestClass]
public sealed class BlowfishTests
{
    private const string ChainKey = "0123456789ABCDEFF0E1D2C3B4A59687";
    private const string ChainVector = "FEDCBA9876543210";

    // "7654321 Now is the time for " with its trailing NUL, zero-padded to four blocks.
    private const string ChainPlaintext = "37363534333231204E6F77206973207468652074696D6520666F722000000000";
    private const string ChainCiphertext = "6B77B4D63006DEE605B156E27403979358DEB9E7154616D959F1652BD5FF92CC";

    // vectors-2.txt, "ecb test data": key, clear, cipher.
    [TestMethod]
    [DataRow("0000000000000000", "0000000000000000", "4EF997456198DD78")]
    [DataRow("FFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", "51866FD5B85ECB8A")]
    [DataRow("3000000000000000", "1000000000000001", "7D856F9A613063F2")]
    [DataRow("1111111111111111", "1111111111111111", "2466DD878B963C9D")]
    [DataRow("0123456789ABCDEF", "1111111111111111", "61F9C3802281B096")]
    [DataRow("1111111111111111", "0123456789ABCDEF", "7D0CC630AFDA1EC7")]
    [DataRow("FEDCBA9876543210", "0123456789ABCDEF", "0ACEAB0FC6A0A28D")]
    [DataRow("7CA110454A1A6E57", "01A1D6D039776742", "59C68245EB05282B")]
    [DataRow("0131D9619DC1376E", "5CD54CA83DEF57DA", "B1B8CC0B250F09A0")]
    [DataRow("07A1133E4A0B2686", "0248D43806F67172", "1730E5778BEA1DA4")]
    [DataRow("3849674C2602319E", "51454B582DDF440A", "A25E7856CF2651EB")]
    [DataRow("04B915BA43FEB5B6", "42FD443059577FA2", "353882B109CE8F1A")]
    [DataRow("0113B970FD34F2CE", "059B5E0851CF143A", "48F4D0884C379918")]
    [DataRow("0170F175468FB5E6", "0756D8E0774761D2", "432193B78951FC98")]
    [DataRow("43297FAD38E373FE", "762514B829BF486A", "13F04154D69D1AE5")]
    [DataRow("07A7137045DA2A16", "3BDD119049372802", "2EEDDA93FFD39C79")]
    [DataRow("04689104C2FD3B2F", "26955F6835AF609A", "D887E0393C2DA6E3")]
    [DataRow("37D06BB516CB7546", "164D5E404F275232", "5F99D04F5B163969")]
    [DataRow("1F08260D1AC2465E", "6B056E18759F5CCA", "4A057A3B24D3977B")]
    [DataRow("584023641ABA6176", "004BD6EF09176062", "452031C1E4FADA8E")]
    [DataRow("025816164629B007", "480D39006EE762F2", "7555AE39F59B87BD")]
    [DataRow("49793EBC79B3258F", "437540C8698F3CFA", "53C55F9CB49FC019")]
    [DataRow("4FB05E1515AB73A7", "072D43A077075292", "7A8E7BFA937E89A3")]
    [DataRow("49E95D6D4CA229BF", "02FE55778117F12A", "CF9C5D7A4986ADB5")]
    [DataRow("018310DC409B26D6", "1D9D5C5018F728C2", "D1ABB290658BC778")]
    [DataRow("1C587F1C13924FEF", "305532286D6F295A", "55CB3774D13EF201")]
    [DataRow("0101010101010101", "0123456789ABCDEF", "FA34EC4847B268B2")]
    [DataRow("1F1F1F1F0E0E0E0E", "0123456789ABCDEF", "A790795108EA3CAE")]
    [DataRow("E0FEE0FEF1FEF1FE", "0123456789ABCDEF", "C39E072D9FAC631D")]
    [DataRow("0000000000000000", "FFFFFFFFFFFFFFFF", "014933E0CDAFF6E4")]
    [DataRow("FFFFFFFFFFFFFFFF", "0000000000000000", "F21E9A77B71C49BC")]
    [DataRow("0123456789ABCDEF", "0000000000000000", "245946885754369A")]
    [DataRow("FEDCBA9876543210", "FFFFFFFFFFFFFFFF", "6B5C5A9C5D9E0A5A")]
    public void EncryptBlockAndDecryptBlock_EcbVector_GiveThePublishedCipherAndClearBytes(string key, string clear, string cipher)
    {
        using Blowfish blowfish = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Blowfish.BlockSize];
        byte[] decrypted = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString(clear), encrypted);
        blowfish.DecryptBlock(Convert.FromHexString(cipher), decrypted);

        Assert.AreEqual(cipher, Convert.ToHexString(encrypted));
        Assert.AreEqual(clear, Convert.ToHexString(decrypted));
    }

    // vectors-2.txt, "set_key test data": data FEDCBA9876543210, key k[n].
    [TestMethod]
    [DataRow("F0", "F9AD597C49DB005E")]
    [DataRow("F0E1", "E91D21C1D961A6D6")]
    [DataRow("F0E1D2", "E9C2B70A1BC65CF3")]
    [DataRow("F0E1D2C3", "BE1E639408640F05")]
    [DataRow("F0E1D2C3B4", "B39E44481BDB1E6E")]
    [DataRow("F0E1D2C3B4A5", "9457AA83B1928C0D")]
    [DataRow("F0E1D2C3B4A596", "8BB77032F960629D")]
    [DataRow("F0E1D2C3B4A59687", "E87A244E2CC85E82")]
    [DataRow("F0E1D2C3B4A5968778", "15750E7A4F4EC577")]
    [DataRow("F0E1D2C3B4A596877869", "122BA70B3AB64AE0")]
    [DataRow("F0E1D2C3B4A5968778695A", "3A833C9AFFC537F6")]
    [DataRow("F0E1D2C3B4A5968778695A4B", "9409DA87A90F6BF2")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C", "884F80625060B8B4")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D", "1F85031C19E11968")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E", "79D9373A714CA34F")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F", "93142887EE3BE15C")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F00", "03429E838CE2D14B")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F0011", "A4299E27469FF67B")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F001122", "AFD5AED1C1BC96A8")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F00112233", "10851C0E3858DA9F")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F0011223344", "E6F51ED79B9DB21F")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F001122334455", "64A6E14AFD36B46F")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F00112233445566", "80C7D7D45A5479AD")]
    [DataRow("F0E1D2C3B4A5968778695A4B3C2D1E0F0011223344556677", "05044B62FA52D080")]
    public void EncryptBlock_SetKeyVector_GivesThePublishedCipherBytes(string key, string cipher)
    {
        using Blowfish blowfish = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Blowfish.BlockSize];

        blowfish.EncryptBlock(Convert.FromHexString("FEDCBA9876543210"), encrypted);

        Assert.AreEqual(cipher, Convert.ToHexString(encrypted));
    }

    // vectors-2.txt, "chaining mode test data", "cbc cipher text".
    [TestMethod]
    public void EncryptCbc_ChainingModeVector_GivesThePublishedCipherText()
    {
        using Blowfish blowfish = new(Convert.FromHexString(ChainKey));
        byte[] ciphertext = new byte[ChainPlaintext.Length / 2];

        blowfish.EncryptCbc(Convert.FromHexString(ChainVector), Convert.FromHexString(ChainPlaintext), ciphertext);

        Assert.AreEqual(ChainCiphertext, Convert.ToHexString(ciphertext));
    }

    // vectors-2.txt, "chaining mode test data", decrypted.
    [TestMethod]
    public void DecryptCbc_ChainingModeVector_GivesThePublishedData()
    {
        using Blowfish blowfish = new(Convert.FromHexString(ChainKey));
        byte[] plaintext = new byte[ChainCiphertext.Length / 2];

        blowfish.DecryptCbc(Convert.FromHexString(ChainVector), Convert.FromHexString(ChainCiphertext), plaintext);

        Assert.AreEqual(ChainPlaintext, Convert.ToHexString(plaintext));
    }

    [TestMethod]
    public void EncryptCbcThenDecryptCbc_InPlaceAndChainedAcrossTwoCalls_RoundTrips()
    {
        using Blowfish blowfish = new(Convert.FromHexString(ChainKey));
        byte[] buffer = Convert.FromHexString(ChainPlaintext);
        byte[] vector = Convert.FromHexString(ChainVector);

        blowfish.EncryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        blowfish.EncryptCbc(buffer.AsSpan(8, 8), buffer.AsSpan(16), buffer.AsSpan(16));
        Assert.AreEqual(ChainCiphertext, Convert.ToHexString(buffer));

        byte[] secondVector = buffer[8..16];
        blowfish.DecryptCbc(vector, buffer.AsSpan(0, 16), buffer.AsSpan(0, 16));
        blowfish.DecryptCbc(secondVector, buffer.AsSpan(16), buffer.AsSpan(16));
        Assert.AreEqual(ChainPlaintext, Convert.ToHexString(buffer));
    }

    [TestMethod]
    public void EncryptCbc_EmptySource_WritesNothing()
    {
        using Blowfish blowfish = new(Convert.FromHexString(ChainKey));

        blowfish.EncryptCbc(Convert.FromHexString(ChainVector), [], []);
        blowfish.DecryptCbc(Convert.FromHexString(ChainVector), [], []);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(57)]
    public void Constructor_KeyOutsideOneTo56Bytes_ThrowsArgumentException(int length)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Blowfish(new byte[length]));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_56ByteKey_IsAccepted()
    {
        using Blowfish blowfish = new(new byte[Blowfish.MaximumKeySize]);
    }

    [TestMethod]
    [DataRow(7, 8, "source")]
    [DataRow(8, 9, "destination")]
    public void EncryptBlockAndDecryptBlock_WrongLength_ThrowArgumentException(int sourceLength, int destinationLength, string parameterName)
    {
        using Blowfish blowfish = new(new byte[16]);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => blowfish.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => blowfish.DecryptBlock(new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    [DataRow(8, 12, 12, "source")]
    [DataRow(8, 16, 8, "destination")]
    [DataRow(7, 16, 16, "initializationVector")]
    public void EncryptCbcAndDecryptCbc_WrongLength_ThrowArgumentException(int vectorLength, int sourceLength, int destinationLength, string parameterName)
    {
        using Blowfish blowfish = new(new byte[16]);

        ArgumentException encrypting = Assert.ThrowsExactly<ArgumentException>(() => blowfish.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));
        ArgumentException decrypting = Assert.ThrowsExactly<ArgumentException>(() => blowfish.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[destinationLength]));

        Assert.AreEqual(parameterName, encrypting.ParamName);
        Assert.AreEqual(parameterName, decrypting.ParamName);
    }

    [TestMethod]
    public void EveryOperation_AfterDispose_ThrowsObjectDisposedException()
    {
        Blowfish blowfish = new(new byte[16]);
        blowfish.Dispose();
        byte[] block = new byte[Blowfish.BlockSize];

        Assert.ThrowsExactly<ObjectDisposedException>(() => blowfish.EncryptBlock(block, block));
        Assert.ThrowsExactly<ObjectDisposedException>(() => blowfish.DecryptBlock(block, block));
        Assert.ThrowsExactly<ObjectDisposedException>(() => blowfish.EncryptCbc(block, block, block));
        Assert.ThrowsExactly<ObjectDisposedException>(() => blowfish.DecryptCbc(block, block, block));
    }
}
