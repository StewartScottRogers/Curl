namespace Curl.Api.Callbacks;

/// <summary>The type of an SSH host key: libcurl's <c>enum curl_khtype</c> (<c>CURLKHTYPE_*</c>), with upstream curl 8.22.0's numbers.</summary>
public enum SshKeyType
{
    /// <summary><c>CURLKHTYPE_UNKNOWN</c> (0): an unknown key type.</summary>
    Unknown = 0,

    /// <summary><c>CURLKHTYPE_RSA1</c> (1): an SSH-1 RSA key.</summary>
    Rsa1 = 1,

    /// <summary><c>CURLKHTYPE_RSA</c> (2): an RSA key.</summary>
    Rsa = 2,

    /// <summary><c>CURLKHTYPE_DSS</c> (3): a DSS key.</summary>
    Dss = 3,

    /// <summary><c>CURLKHTYPE_ECDSA</c> (4): an ECDSA key.</summary>
    Ecdsa = 4,

    /// <summary><c>CURLKHTYPE_ED25519</c> (5): an Ed25519 key.</summary>
    Ed25519 = 5,
}
