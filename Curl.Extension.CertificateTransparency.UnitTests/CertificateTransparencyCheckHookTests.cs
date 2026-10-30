using System.Text;

namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class CertificateTransparencyCheckHookTests
{
    [TestMethod]
    public async Task RefusalOfAsync_Rfc6962Section5_2_ValidTimestampFromTheListedLog_LetsTheChainStand()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));
        var logList = CertificateTransparencyChain.LogListOf(chain.LogKey.ExportSubjectPublicKeyInfo());
        string? readPath = null;
        var hook = new CertificateTransparencyCheckHook("logs.json", (path, _) => { readPath = path; return Task.FromResult(logList); }, new FixedTimeProvider(CertificateTransparencyChain.Logged.AddDays(1)));

        var refusal = await hook.RefusalOfAsync([leaf, chain.IssuerDer], "example.com", 443, CancellationToken.None);

        Assert.IsNull(refusal);
        Assert.AreEqual("logs.json", readPath);
        Assert.AreSame(CertificateTransparencyExtension.Option, hook.Option);
    }

    [TestMethod]
    public async Task RefusalOfAsync_Rfc9162Section8_1_3_TimestampLaterThanTheClock_IsRefused()
    {
        using var chain = new CertificateTransparencyChain();
        var leaf = chain.LeafWith(CertificateTransparencyChain.ListOf(chain.EcdsaTimestamp(CertificateTransparencyChain.Logged)));
        var logList = CertificateTransparencyChain.LogListOf(chain.LogKey.ExportSubjectPublicKeyInfo());
        var hook = new CertificateTransparencyCheckHook("logs.json", (_, _) => Task.FromResult(logList), new FixedTimeProvider(CertificateTransparencyChain.Logged.AddSeconds(-1)));

        var refusal = await hook.RefusalOfAsync([leaf, chain.IssuerDer], "example.com", 443, CancellationToken.None);

        Assert.AreEqual(SignedCertificateTimestampVerifier.NoValidTimestamp, refusal);
    }

    [TestMethod]
    public async Task RefusalOfAsync_LogListUnreadable_IsRefusedNamingTheFile()
    {
        var hook = new CertificateTransparencyCheckHook("missing.json", (_, _) => throw new FileNotFoundException("gone"), TimeProvider.System);

        var refusal = await hook.RefusalOfAsync([], "example.com", 443, CancellationToken.None);

        Assert.AreEqual("Certificate Transparency: the log list missing.json could not be read: gone", refusal);
    }

    [TestMethod]
    public async Task RefusalOfAsync_LogListNotPermitted_IsRefusedNamingTheFile()
    {
        var hook = new CertificateTransparencyCheckHook("locked.json", (_, _) => throw new UnauthorizedAccessException("denied"), TimeProvider.System);

        var refusal = await hook.RefusalOfAsync([], "example.com", 443, CancellationToken.None);

        Assert.AreEqual("Certificate Transparency: the log list locked.json could not be read: denied", refusal);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("{\"operators\":[{\"logs\":[{\"key\":\"!\"}]}]}")]
    public async Task RefusalOfAsync_LogListIsNotJsonOrHasAKeyNotInBase64_IsRefusedNamingTheFile(string json)
    {
        var hook = new CertificateTransparencyCheckHook("bad.json", (_, _) => Task.FromResult(Encoding.UTF8.GetBytes(json)), TimeProvider.System);

        var refusal = await hook.RefusalOfAsync([], "example.com", 443, CancellationToken.None);

        StringAssert.StartsWith(refusal, "Certificate Transparency: the log list bad.json could not be read: ");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
