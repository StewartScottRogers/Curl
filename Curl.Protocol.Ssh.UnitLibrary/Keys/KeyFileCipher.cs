namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// A CBC cipher an encrypted PEM or PKCS #8 key file can name.
/// </summary>
internal enum KeyFileCipher
{
    /// <summary>AES with a 128-bit key.</summary>
    Aes128,

    /// <summary>AES with a 192-bit key.</summary>
    Aes192,

    /// <summary>AES with a 256-bit key.</summary>
    Aes256,

    /// <summary>Three-key triple DES (<c>DES-EDE3-CBC</c>).</summary>
    TripleDes,

    /// <summary>Single DES (<c>DES-CBC</c>).</summary>
    Des,
}
