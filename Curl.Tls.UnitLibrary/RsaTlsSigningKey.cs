using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An RSA key that signs with RSA-PSS (the <c>rsa_pss_rsae_*</c> schemes, or
/// <c>rsa_pss_pss_*</c> for a key certified as RSASSA-PSS), in TLS 1.2 with PKCS #1 v1.5
/// (<c>rsa_pkcs1_*</c>), and in TLS 1.0 and 1.1 with the PKCS #1 block over MD5 and SHA-1
/// that carries no DigestInfo (RFC 2246 section 7.4.3). The BCL cannot make that one, nor
/// <c>rsa_pkcs1_sha224</c> (it does not compute SHA-224), so for those the key exports its private parameters and signs it with
/// <see cref="RsaCrtPrivateKey" />; a key whose private parameters cannot be exported
/// cannot sign it.
/// </summary>
/// <param name="key">The RSA private key.</param>
/// <param name="certifiedAsPss"><see langword="true" /> when the certificate names the key <c>id-RSASSA-PSS</c> rather than <c>rsaEncryption</c>.</param>
public sealed class RsaTlsSigningKey(RSA key, bool certifiedAsPss = false) : TlsSigningKey
{
    private bool? privateParametersExportable;

    private protected override bool Fits(TlsSignatureRule rule) =>
        rule.KeyOid == (certifiedAsPss ? TlsSignatureScheme.RsaSsaPssOid : TlsSignatureScheme.RsaEncryptionOid)
        && (!IsHandBuilt(rule.Kind) || CanExportPrivateParameters());

    private protected override byte[] Sign(TlsSignatureRule rule, byte[] content) => rule.Kind switch
    {
        TlsSignatureKind.RsaMd5Sha1 or TlsSignatureKind.RsaPkcs1Sha224 => SignHandBuiltPkcs1(rule.Kind, content),
        TlsSignatureKind.RsaPkcs1 => key.SignData(content, rule.Hash, RSASignaturePadding.Pkcs1),
        _ => key.SignData(content, rule.Hash, RSASignaturePadding.Pss),
    };

    private static void ZeroPrivateParameters(RSAParameters parameters)
    {
        CryptographicOperations.ZeroMemory(parameters.D);
        CryptographicOperations.ZeroMemory(parameters.P);
        CryptographicOperations.ZeroMemory(parameters.Q);
        CryptographicOperations.ZeroMemory(parameters.DP);
        CryptographicOperations.ZeroMemory(parameters.DQ);
        CryptographicOperations.ZeroMemory(parameters.InverseQ);
    }

    /// <summary>Returns, once and then from memory, whether the key's private parameters can be exported.</summary>
    private bool CanExportPrivateParameters()
    {
        privateParametersExportable ??= TryExportPrivateParameters();
        return privateParametersExportable.Value;
    }

    private bool TryExportPrivateParameters()
    {
        try
        {
            ZeroPrivateParameters(key.ExportParameters(includePrivateParameters: true));
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool IsHandBuilt(TlsSignatureKind kind) => kind is TlsSignatureKind.RsaMd5Sha1 or TlsSignatureKind.RsaPkcs1Sha224;

    /// <summary>
    /// Signs a PKCS #1 type 1 block the BCL cannot make - RFC 2246 section 7.4.3's over
    /// MD5(content) || SHA-1(content), or <c>rsa_pkcs1_sha224</c>'s over SHA-224's
    /// DigestInfo - with the raw private operation.
    /// </summary>
    private byte[] SignHandBuiltPkcs1(TlsSignatureKind kind, byte[] content)
    {
        RSAParameters parameters = key.ExportParameters(includePrivateParameters: true);
        try
        {
            using RsaCrtPrivateKey privateKey = new(parameters);
            byte[] block = TlsSignatureScheme.BuildHandBuiltPkcs1Block(kind, content, privateKey.ModulusLength);
            byte[] signature = new byte[privateKey.ModulusLength];
            privateKey.ApplyPrivateExponent(block, signature);
            return signature;
        }
        finally
        {
            ZeroPrivateParameters(parameters);
        }
    }
}
