using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-095 (BL-3624, ADR-0948): curl 8.18.0's OpenSSL build asks for the pass phrase of an
/// encrypted PEM key given without <c>--pass</c>, writing <c>Enter PEM pass phrase:</c> to
/// standard error: once when a line is read, twice at the end of the input before exit 43.
/// </summary>
[TestClass]
public sealed class Cgr095Tests : IDisposable
{
    private const string PassPhrase = "secret";

    private const string Prompt = "Enter PEM pass phrase:\n";

    private readonly string _directory = Directory.CreateTempSubdirectory("cgr095-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void LoadAsOpenSslBuild_EncryptedKeyWithoutPassphraseAtEndOfInput_PromptsTwiceAndFailsWithExit43()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();
        using var promptOutput = new StringWriter();
        var prompt = new TerminalPemPassPhrasePrompt(promptOutput, () => null);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null, prompt);

        Assert.IsNull(loaded);
        Assert.AreEqual(Prompt + Prompt, promptOutput.ToString());
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
        Assert.AreEqual($"unable to set private key file: '{keyFile}' type PEM", failure.ErrorMessage);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_EncryptedKeyWithoutPassphraseAnsweredRightly_PromptsOnceAndLoadsTheKey()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();
        using var promptOutput = new StringWriter();
        var prompt = new TerminalPemPassPhrasePrompt(promptOutput, () => PassPhrase);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null, prompt);

        using (loaded)
        {
            Assert.IsNull(failure);
            Assert.IsTrue(loaded!.HasPrivateKey);
            Assert.AreEqual(Prompt, promptOutput.ToString());
        }
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_EncryptedKeyWithoutPassphraseAnsweredWrongly_PromptsOnceAndFailsWithExit43()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();
        var asked = 0;
        var prompt = new TerminalPemPassPhrasePrompt(TextWriter.Null, () => { asked++; return "wrong"; });

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null, prompt);

        Assert.IsNull(loaded);
        Assert.AreEqual(1, asked);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_OptionsWithAPrompt_AskThroughIt()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();
        var prompt = new TerminalPemPassPhrasePrompt(TextWriter.Null, () => PassPhrase);
        var options = new TlsClientOptions(ClientCertificate: certificateFile, PrivateKey: keyFile, PemPassPhrasePrompt: prompt);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(options);

        using (loaded)
        {
            Assert.IsNull(failure);
            Assert.IsTrue(loaded!.HasPrivateKey);
        }
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_PassphraseGiven_AsksNothing()
    {
        var (certificateFile, keyFile) = WriteCertificateAndEncryptedKey();

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, PassPhrase, keyFile, null, null, new FailingPrompt());

        using (loaded)
        {
            Assert.IsNull(failure);
            Assert.IsTrue(loaded!.HasPrivateKey);
        }
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_UnencryptedKey_AsksNothing()
    {
        using var key = RSA.Create(2048);
        var certificateFile = WriteCertificate(key);
        var keyFile = WriteFile("plain.key", key.ExportPkcs8PrivateKeyPem());

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null, new FailingPrompt());

        using (loaded)
        {
            Assert.IsNull(failure);
            Assert.IsTrue(loaded!.HasPrivateKey);
        }
    }

    [TestMethod]
    [DataRow("DER", "enc.key")]
    [DataRow(null, "missing.key")]
    public void LoadAsOpenSslBuild_DerKeyTypeOrMissingKeyFile_AsksNothingAndFailsWithExit43(string? privateKeyType, string keyName)
    {
        var (certificateFile, _) = WriteCertificateAndEncryptedKey();
        var keyFile = Path.Combine(_directory, keyName);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, privateKeyType, new FailingPrompt());

        Assert.IsNull(loaded);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_TraditionalEncryptedPemKeyAtEndOfInput_PromptsTwice()
    {
        var (certificateFile, _) = WriteCertificateAndEncryptedKey();
        var keyFile = WriteFile(
            "legacy.key",
            "-----BEGIN RSA PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nDEK-Info: AES-256-CBC,00000000000000000000000000000000\n\nAAAA\n-----END RSA PRIVATE KEY-----\n");
        var asked = 0;
        var prompt = new TerminalPemPassPhrasePrompt(TextWriter.Null, () => { asked++; return null; });

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null, prompt);

        Assert.IsNull(loaded);
        Assert.AreEqual(2, asked);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
    }

    private (string CertificateFile, string KeyFile) WriteCertificateAndEncryptedKey()
    {
        using var key = RSA.Create(2048);
        var encryption = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2048);
        return (WriteCertificate(key), WriteFile("enc.key", key.ExportEncryptedPkcs8PrivateKeyPem(PassPhrase, encryption)));
    }

    private string WriteCertificate(RSA key)
    {
        var request = new CertificateRequest("CN=cli", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return WriteFile("cli.crt", certificate.ExportCertificatePem());
    }

    private string WriteFile(string name, string contents)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private sealed class FailingPrompt : IPemPassPhrasePrompt
    {
        public string? AskPassPhrase() => throw new AssertFailedException("The pass phrase was asked for.");
    }
}
