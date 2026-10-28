using System.Net.Security;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-168 (BL-3697): when a <c>--crlfile</c> list refuses the server's certificate, the OpenSSL
/// build's observation carries the list's verify result, so <c>%{ssl_verify_result}</c> reports
/// what the exit 60 message names. Measured on 2026-10-28 with curl 8.18.0 (OpenSSL 3.5.5) against
/// <c>openssl s_server -WWW</c> and <c>-sS --cacert rca.crt --crlfile &lt;list&gt; -o out -w
/// '%{ssl_verify_result} %{exitcode}\n'</c>: a list revoking the certificate prints <c>23 60</c> and
/// <c>curl: (60) SSL certificate OpenSSL verify result: certificate revoked (23)</c>; an expired
/// list prints <c>12 60</c> and <c>... CRL has expired (12)</c>; neither writes <c>out</c>.
/// </summary>
[TestClass]
public sealed class Cgr168Tests
{
    private const bool OpenSslBuild = false;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Judge_ListRevokingTheCertificateInTheOpenSslBuild_ObservesVerifyResult23()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root);

        var (observed, failure) = Judge(root, leaf, RevokingList(root, leaf));

        Assert.AreEqual(23L, observed.VerifyResult);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate revoked (23)", failure?.Message);
    }

    [TestMethod]
    public void Judge_ExpiredListInTheOpenSslBuild_ObservesVerifyResult12()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root);

        var (observed, failure) = Judge(root, leaf, ExpiredList(root));

        Assert.AreEqual(12L, observed.VerifyResult);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: CRL has expired (12)", failure?.Message);
    }

    [TestMethod]
    public void Judge_ListAcceptingTheCertificateInTheOpenSslBuild_ObservesVerifyResult0()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root);

        var (observed, failure) = Judge(root, leaf, RevokingList(root, revoked: null));

        Assert.AreEqual(0L, observed.VerifyResult);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void ReportVerifyResult_AfterARevokingListInTheOpenSslBuild_Reports23()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root);
        var events = new RecordingTransferEvents();
        var (observed, _) = Judge(root, leaf, RevokingList(root, leaf));

        observed.ReportVerifyResult(events, isProxy: false, matchesSchannelBuild: OpenSslBuild);

        Assert.AreEqual((23L, false), events.VerifyResults.Single());
    }

    private static X509Certificate2 CreateRoot() =>
        CertificateRevocationListTests.CreateAuthority("CN=CGR168 Root", RSA.Create(2048));

    private static X509Certificate2 CreateLeaf(X509Certificate2 root)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        return request.Create(root, Now.AddDays(-1), Now.AddDays(1), [5]);
    }

    private static string RevokingList(X509Certificate2 root, X509Certificate2? revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
        {
            builder.AddEntry(revoked);
        }

        return TestRevocationList.Pem(builder.Build(root, BigInteger.One, Now.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, Now.AddHours(-1)));
    }

    // Made as `openssl ca -gencrl -crlsec 1` makes it, then read once it has run out.
    private static string ExpiredList(X509Certificate2 root) =>
        TestRevocationList.Pem(TestRevocationList.Write(root, thisUpdate: Now.AddHours(-1), nextUpdate: Now.AddMinutes(-1)));

    private (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Judge(
        X509Certificate2 root, X509Certificate2 leaf, string listPem)
    {
        var listFile = Path.Combine(_directory, "list.crl");
        File.WriteAllText(listFile, listPem);
        var options = new TlsClientOptions(CaCertificateFile: "rca.crt", CertificateRevocationListFile: listFile);
        var verification = new ServerCertificateVerification(options, OpenSslBuild, TimeProvider.System);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.Build(leaf);
        return verification.Judge(
            SslPolicyErrors.None, chain, "localhost", [], CertificateRevocationListFile.Load(listFile), [new ReadOnlyMemory<byte>(leaf.RawData)]);
    }
}
