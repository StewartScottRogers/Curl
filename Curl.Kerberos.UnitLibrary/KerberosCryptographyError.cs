namespace Curl.Kerberos;

/// <summary>Why a Kerberos encryption, decryption or string-to-key could not be done.</summary>
public enum KerberosCryptographyError
{
    /// <summary>The encryption type is not one <see cref="KerberosEncryption.Create" /> knows.</summary>
    UnsupportedEncryptionType,

    /// <summary>The ciphertext is shorter than the confounder and checksum it must carry.</summary>
    CiphertextTooShort,

    /// <summary>The ciphertext's checksum does not match its contents: it was altered, or the key or key usage is wrong.</summary>
    IntegrityCheckFailed,

    /// <summary>
    /// The string-to-key parameters are not four bytes, or name an iteration count of
    /// 2^24 or more, which MIT Kerberos refuses too (<c>KRB5_ERR_BAD_S2K_PARAMS</c>).
    /// </summary>
    BadStringToKeyParameters,

    /// <summary>
    /// The encrypted part of a block-cipher ciphertext is not a whole number of blocks, which
    /// MIT Kerberos refuses too (<c>KRB5_BAD_MSIZE</c>).
    /// </summary>
    CiphertextNotWholeBlocks,

    /// <summary>
    /// The <c>des3-cbc-sha1</c> key, or a key derived from it, is one .NET's triple DES calls
    /// weak: its first and second, or second and third, 8-byte parts are equal, which makes
    /// it single DES. MIT Kerberos would use it as given; Curl refuses it (ADR-0423).
    /// </summary>
    WeakKey,
}
