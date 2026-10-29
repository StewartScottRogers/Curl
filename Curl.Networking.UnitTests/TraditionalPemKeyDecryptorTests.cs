using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// BL-3980: OpenSSL's traditional encrypted PEM keys, as <c>openssl ec -&lt;cipher&gt;
/// -passout pass:secret</c> (OpenSSL 3.5.5) wrote them from <see cref="PlainKey" />, decrypt to
/// that key, and the OpenSSL build loads one with <c>--key</c> and <c>--pass</c>.
/// </summary>
[TestClass]
public sealed class TraditionalPemKeyDecryptorTests : IDisposable
{
    private const string PassPhrase = "secret";

    private const string PlainKey =
        "-----BEGIN EC PRIVATE KEY-----\n" +
        "MHcCAQEEIPZbNkp4vsKa19V/mbzeAb8Oq6ijlDbq7Bz24Tt6HVG4oAoGCCqGSM49\n" +
        "AwEHoUQDQgAEW4ubS6brZGrJMyd3wypFCcu3c8UPR1rusM/vJm1SW6AoGfd7GLSw\n" +
        "KCe60Hp11Clbi+IvaAbcJCc/owGS5POGjQ==\n" +
        "-----END EC PRIVATE KEY-----\n";

    private const string Aes128Key =
        "-----BEGIN EC PRIVATE KEY-----\n" +
        "Proc-Type: 4,ENCRYPTED\n" +
        "DEK-Info: AES-128-CBC,187A1E3F0826348FA03230331B10EE09\n" +
        "\n" +
        "NSamY//0tnehu4W7HiXis2rRU00xsDVDl3bcnxXxAazgFYpf5ySLOBZRVH8iTevd\n" +
        "6WIU8GPlw9j4OZrKzd9AXhaDzYUQbRMyZdn+jQzl/wVKVKoh4DESnR0eWO4JRQhV\n" +
        "a+yPgkn0ld3eFoQyNq2PvETe22fKa9bm+ner9VqRZoE=\n" +
        "-----END EC PRIVATE KEY-----\n";

    private const string Aes192Key =
        "-----BEGIN EC PRIVATE KEY-----\n" +
        "Proc-Type: 4,ENCRYPTED\n" +
        "DEK-Info: AES-192-CBC,BCC5F1636F52B0507E17B0E2216DBA9B\n" +
        "\n" +
        "dxOiGXG1CxkpDTGh+QwxQUA8ppO9sK6KgxohxwsoJDW5eP7EnDeVYsLybKB8q+hq\n" +
        "qkXQmyg4K3A5kNO4jPtHblM1eIqTea+XHOlon/e0mLanx+N5XoxObAozGTcoEmC+\n" +
        "RDFI0rWfTAk/zmrorqzk1sRUinMzC0JlHkg/TWAFf4A=\n" +
        "-----END EC PRIVATE KEY-----\n";

    private const string Aes256Key =
        "-----BEGIN EC PRIVATE KEY-----\n" +
        "Proc-Type: 4,ENCRYPTED\n" +
        "DEK-Info: AES-256-CBC,FB4096C4ED1A1121E9A418071AC60DEE\n" +
        "\n" +
        "tW6ZFVyfpOJBDZNm1CuUz3nNFfMNr7lEBQrC7Q9l7s3lbOxGj7xR1GnLR2qgbQOd\n" +
        "UnYILhl+flTfi4RZ1HN6U6wVHDLrVmYYkuhCaLbyaVQa1jRHCtkhtpHj8Fi/UtN+\n" +
        "EZRmgqYEQOC60mpZwm1Qb9pJldZdAtViQDm8wGnaZvs=\n" +
        "-----END EC PRIVATE KEY-----\n";

    private const string DesEde3Key =
        "-----BEGIN EC PRIVATE KEY-----\n" +
        "Proc-Type: 4,ENCRYPTED\n" +
        "DEK-Info: DES-EDE3-CBC,E8CEB72C5BF90CD9\n" +
        "\n" +
        "JuRmgXtEy/hJKSGqSfq8NstWYjHqmF4CMhM0ov72qLD2jPhRPXJp2YLZpwu1mDoF\n" +
        "C5NKGrlw1AxnOkRT6EQ5WSKvmsLIT6FwWiJ7FJ7Xq8DFvqyRTqkvfWQye20ibbFO\n" +
        "L8FkVOEA423y3SXM2fiQk5cC7wcGbitXwc5pNOoqGxw=\n" +
        "-----END EC PRIVATE KEY-----\n";

    private readonly string _directory = Directory.CreateTempSubdirectory("bl3980-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(Aes128Key, DisplayName = "AES-128-CBC")]
    [DataRow(Aes192Key, DisplayName = "AES-192-CBC")]
    [DataRow(Aes256Key, DisplayName = "AES-256-CBC")]
    [DataRow(DesEde3Key, DisplayName = "DES-EDE3-CBC")]
    public void Decrypt_KeyOpenSslEncrypted_IsThePlainKey(string encryptedKey)
    {
        Assert.AreEqual(PlainKey.TrimEnd('\n'), TraditionalPemKeyDecryptor.Decrypt(encryptedKey, PassPhrase));
    }

    [TestMethod]
    public void Decrypt_KeyWithCrlfLineEndsAfterAnotherBlock_IsThePlainKey()
    {
        var file = ("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n" + Aes256Key).ReplaceLineEndings("\r\n");

        Assert.AreEqual(PlainKey.TrimEnd('\n'), TraditionalPemKeyDecryptor.Decrypt(file, PassPhrase));
    }

    [TestMethod]
    public void Decrypt_WrongPassPhrase_Throws()
    {
        Assert.ThrowsExactly<CryptographicException>(() => TraditionalPemKeyDecryptor.Decrypt(Aes256Key, "wrong"));
    }

    [TestMethod]
    [DataRow(PlainKey, DisplayName = "no Proc-Type")]
    [DataRow("Proc-Type: 4,ENCRYPTED\n\nAAAA\n", DisplayName = "no BEGIN line")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,187A1E3F0826348FA03230331B10EE09\n\nAAAA\n", DisplayName = "no END line")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,187A1E3F0826348FA03230331B10EE09\nAAAA\n-----END EC PRIVATE KEY-----\n", DisplayName = "no blank line")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nAAAA\n-----END EC PRIVATE KEY-----\n\n", DisplayName = "blank line after END")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\n\nAAAA\n-----END EC PRIVATE KEY-----\n", DisplayName = "no DEK-Info")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC\n\nAAAA\n-----END EC PRIVATE KEY-----\n", DisplayName = "no IV")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,187A1E\n\nAAAA\n-----END EC PRIVATE KEY-----\n", DisplayName = "IV too short")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,XYZ\n\nAAAA\n-----END EC PRIVATE KEY-----\n", DisplayName = "IV not hex")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-128-CBC,187A1E3F0826348FA03230331B10EE09\n\n!!!!\n-----END EC PRIVATE KEY-----\n", DisplayName = "body not base64")]
    [DataRow("-----BEGIN EC PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: DES-CBC,E8CEB72C5BF90CD9\n\nAAAAAAAAAAA=\n-----END EC PRIVATE KEY-----\n", DisplayName = "cipher not supported")]
    public void Decrypt_BlockThatDoesNotParse_Throws(string keyPem)
    {
        Assert.ThrowsExactly<CryptographicException>(() => TraditionalPemKeyDecryptor.Decrypt(keyPem, PassPhrase));
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_TraditionalEncryptedKeyWithRightPass_LoadsTheKey()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, PassPhrase, keyFile, null, null);

        Assert.IsNull(failure);
        using (loaded)
        {
            Assert.IsTrue(loaded!.HasPrivateKey);
        }
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_TraditionalEncryptedKeyWithWrongPass_FailsWithExit43()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, "wrong", keyFile, null, null);

        Assert.IsNull(loaded);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
        Assert.AreEqual($"unable to set private key file: '{keyFile}' type PEM", failure.ErrorMessage);
    }

    private (string CertificateFile, string KeyFile) WriteCertificateAndEncryptedKey()
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(PlainKey);
        var request = new CertificateRequest("CN=cli", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificateFile = Path.Combine(_directory, "cli.crt");
        var keyFile = Path.Combine(_directory, "cli_trad_enc.key");
        File.WriteAllText(certificateFile, certificate.ExportCertificatePem());
        File.WriteAllText(keyFile, Aes256Key);
        return (certificateFile, keyFile);
    }
}
