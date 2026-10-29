using System.Security.Cryptography;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Decrypts a key in OpenSSL's traditional encrypted PEM form (a <c>Proc-Type: 4,ENCRYPTED</c>
/// block with a <c>DEK-Info: &lt;cipher&gt;,&lt;IV hex&gt;</c> header), which the OpenSSL build
/// of curl reads and the BCL does not: the cipher's key is OpenSSL's <c>EVP_BytesToKey</c> of
/// the pass phrase (MD5, one iteration, the IV's first eight bytes as salt).
/// </summary>
internal static class TraditionalPemKeyDecryptor
{
    private const string BeginMarker = "-----BEGIN ";
    private const string MarkerEnd = "-----";
    private const string EncryptedProcType = "Proc-Type: 4,ENCRYPTED";
    private const string DekInfoHeader = "DEK-Info: ";
    private const int SaltLength = 8;

    /// <summary>
    /// Returns the first encrypted block of <paramref name="keyPem" /> decrypted with
    /// <paramref name="passphrase" />, as an unencrypted PEM block under the same label.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// No encrypted block, a header that does not parse, an unsupported cipher, or a pass
    /// phrase that does not decrypt it.
    /// </exception>
    public static string Decrypt(string keyPem, string passphrase)
    {
        var text = keyPem.ReplaceLineEndings("\n");
        var headerAt = text.IndexOf(EncryptedProcType, StringComparison.Ordinal);
        var beginAt = headerAt < 0 ? -1 : text.LastIndexOf(BeginMarker, headerAt, StringComparison.Ordinal);
        if (beginAt < 0)
        {
            throw new CryptographicException("The key file holds no traditional encrypted PEM block.");
        }

        var labelAt = beginAt + BeginMarker.Length;
        var label = text[labelAt..text.IndexOf(MarkerEnd, labelAt, StringComparison.Ordinal)];
        var endAt = text.IndexOf($"-----END {label}-----", headerAt, StringComparison.Ordinal);
        var blankAt = text.IndexOf("\n\n", headerAt, StringComparison.Ordinal);
        if (endAt < 0 || blankAt < 0 || blankAt > endAt)
        {
            throw new CryptographicException("The traditional encrypted PEM block does not parse.");
        }

        try
        {
            var (cipher, iv) = ReadDekInfo(text[headerAt..blankAt]);
            var encrypted = Convert.FromBase64String(text[blankAt..endAt]);
            var key = DerivedKey(Encoding.UTF8.GetBytes(passphrase), iv.AsSpan(0, SaltLength), KeyLength(cipher));
            return PemEncoding.WriteString(label, DecryptCbc(cipher, key, iv, encrypted));
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The traditional encrypted PEM block does not parse.", exception);
        }
    }

    private static (string Cipher, byte[] Iv) ReadDekInfo(string headers)
    {
        var dekInfoAt = headers.IndexOf(DekInfoHeader, StringComparison.Ordinal);
        var fields = dekInfoAt < 0 ? [] : headers[(dekInfoAt + DekInfoHeader.Length)..].Split('\n')[0].Split(',');
        var iv = fields.Length == 2 ? Convert.FromHexString(fields[1].Trim()) : [];
        return iv.Length >= SaltLength
            ? (fields[0].Trim(), iv)
            : throw new CryptographicException("The DEK-Info header does not parse.");
    }

    private static int KeyLength(string cipher) => cipher switch
    {
        "AES-128-CBC" => 16,
        "AES-192-CBC" or "DES-EDE3-CBC" => 24,
        "AES-256-CBC" => 32,
        _ => throw new CryptographicException($"The cipher {cipher} is not supported."),
    };

    private static byte[] DecryptCbc(string cipher, byte[] key, byte[] iv, byte[] encrypted)
    {
        using SymmetricAlgorithm algorithm = cipher == "DES-EDE3-CBC" ? TripleDES.Create() : Aes.Create();
        algorithm.Key = key;
        return algorithm.DecryptCbc(encrypted, iv, PaddingMode.PKCS7);
    }

    // EVP_BytesToKey with MD5 and one iteration: D1 = MD5(pass || salt), Dn = MD5(Dn-1 || pass || salt).
    private static byte[] DerivedKey(byte[] passphrase, ReadOnlySpan<byte> salt, int length)
    {
        var key = new List<byte>(length + MD5.HashSizeInBytes);
        var previous = Array.Empty<byte>();
        while (key.Count < length)
        {
            previous = MD5.HashData([.. previous, .. passphrase, .. salt]);
            key.AddRange(previous);
        }

        return [.. key.Take(length)];
    }
}
