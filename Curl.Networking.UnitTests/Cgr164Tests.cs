using System.Net;
using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-164 of the third-party gap report (BL-3693): curl resumes a TLS session only between
/// transfers whose verification settings match, so <c>-k</c> then <c>--cacert</c>, the reverse,
/// or an added <c>--pinnedpubkey</c> makes a new session, and the same options twice resume one
/// (ADR-0859).
/// </summary>
[TestClass]
public sealed class Cgr164Tests
{
    private static readonly TlsClientOptions Insecure = new(Insecure: true);

    private static readonly TlsClientOptions WithCaFile = new(CaCertificateFile: "ca.crt", SkipRevocationCheck: true);

    private static readonly TlsClientOptions WithCaFileAndPin = WithCaFile with { PinnedPublicKey = "sha256//AAAA" };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    public static IEnumerable<object[]> DifferentVerification =>
    [
        [Insecure, WithCaFile],
        [WithCaFile, Insecure],
        [WithCaFile, WithCaFileAndPin],
        [WithCaFile, new TlsClientOptions(CaCertificateDirectory: "certs")],
        [WithCaFile, new TlsClientOptions(CaCertificateBlob: new byte[] { 1 })],
        [new TlsClientOptions(CaCertificateBlob: new byte[] { 1 }), new TlsClientOptions(CaCertificateBlob: new byte[] { 2 })],
        [Insecure, Insecure with { ChecksHostNameWhenInsecure = true }],
        [WithCaFile, WithCaFile with { RequireCertificateStatus = true }],
        [WithCaFile, WithCaFile with { ClientCertificate = "client.pem", PrivateKey = "client.key" }],
    ];

    [TestMethod]
    [DynamicData(nameof(DifferentVerification))]
    public void MayResume_SecondHandshakeToTheHostWithOtherVerification_IsFalse(TlsClientOptions first, TlsClientOptions second)
    {
        SslStreamSessionPartitions partitions = new();
        Diagnostics.Arrange("first", first);
        Diagnostics.Arrange("second", second);

        var firstResumes = partitions.MayResume("localhost", first);
        var secondResumes = partitions.MayResume("localhost", second);

        Diagnostics.Act("MayResume", (firstResumes, secondResumes));
        Assert.IsTrue(firstResumes);
        Assert.IsFalse(secondResumes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AuthenticateAsClientAsync_InsecureThenAnAddedPin_DoesNotResume(bool pinFirst)
    {
        SslStreamSessionPartitions partitions = new();
        TlsClientOptions pinned = Insecure with { PinnedPublicKey = "sha256//AAAA" };

        var firstResumes = await AllowTlsResumeAsync(pinFirst ? pinned : Insecure, partitions);
        var secondResumes = await AllowTlsResumeAsync(pinFirst ? Insecure : pinned, partitions);

        Diagnostics.Act("AllowTlsResume", (firstResumes, secondResumes));
        Assert.IsTrue(firstResumes);
        Assert.IsFalse(secondResumes);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SameVerificationTwice_Resumes()
    {
        SslStreamSessionPartitions partitions = new();

        var firstResumes = await AllowTlsResumeAsync(Insecure, partitions);
        var secondResumes = await AllowTlsResumeAsync(Insecure, partitions);

        Diagnostics.Act("AllowTlsResume", (firstResumes, secondResumes));
        Assert.IsTrue(firstResumes);
        Assert.IsTrue(secondResumes);
    }

    [TestMethod]
    public void MayResume_TheFirstSettingsAgainAfterOthers_Resumes()
    {
        SslStreamSessionPartitions partitions = new();

        var answers = new[]
        {
            partitions.MayResume("localhost", Insecure),
            partitions.MayResume("LOCALHOST", WithCaFile),
            partitions.MayResume("localhost", Insecure),
            partitions.MayResume("example.com", WithCaFile),
        };

        Diagnostics.Act("answers", answers);
        CollectionAssert.AreEqual(new[] { true, false, true, true }, answers);
    }

    private static async Task<bool> AllowTlsResumeAsync(TlsClientOptions options, SslStreamSessionPartitions partitions)
    {
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(options, matchesSchannelBuild: true)
        {
            SessionPartitions = partitions,
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();

        await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, new IPEndPoint(IPAddress.Loopback, 443)), "localhost", CancellationToken.None);

        return handshakeOptions!.AllowTlsResume;
    }
}
