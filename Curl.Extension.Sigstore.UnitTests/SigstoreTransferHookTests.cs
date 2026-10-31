using System.Security.Cryptography;
using System.Text;

namespace Curl.Extension.Sigstore.UnitTests;

[TestClass]
public sealed class SigstoreTransferHookTests
{
    private static readonly byte[] Artifact = Encoding.ASCII.GetBytes("release-1.0.tar.gz contents");

    private readonly string directory = Path.Combine(Path.GetTempPath(), "curl-sigstore-" + Guid.NewGuid().ToString("N"));

    private readonly SigstoreFixture fulcio = new();

    private readonly ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [TestInitialize]
    public void CreateDirectory() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void CleanUp()
    {
        Directory.Delete(directory, recursive: true);
        fulcio.Dispose();
        signer.Dispose();
    }

    [TestMethod]
    public async Task ExecuteAsync_ABundleThatVerifies_WritesTheBodyAndHeaders_CosignVerifyBlob()
    {
        var context = Context();

        var result = await Hook(ValidBundle()).ExecuteAsync(context, Serve(Artifact));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Artifact, Bytes(context.Output));
        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Encoding.ASCII.GetString(Bytes(context.HeaderOutput!)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ADownloadThatDiffers_FailsWith60AndWritesNoBody_BundleMessageDigest()
    {
        var context = Context();

        var result = await Hook(ValidBundle()).ExecuteAsync(context, Serve(Encoding.ASCII.GetBytes("tampered")));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "SHA-256");
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherIdentityExpected_FailsWith60()
    {
        var result = await Hook(ValidBundle(), identity: "someone@example.com").ExecuteAsync(Context(), Serve(Artifact));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "someone@example.com");
    }

    [TestMethod]
    public async Task ExecuteAsync_TheTransferFails_ReturnsItsResultAndWritesNothing()
    {
        var context = Context();

        var result = await Hook(ValidBundle()).ExecuteAsync(context, _ => ValueTask.FromResult(TransferResult.Failure(CurlExitCode.ReadError, "no")));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoIdentityOrNoRoot_FailsWith2BeforeTransferring()
    {
        var bundle = ValidBundle();

        var noIdentity = await new SigstoreTransferHook(new(bundle, null, null, RootFile())).ExecuteAsync(Context(), NoServer);
        var noRoot = await new SigstoreTransferHook(new(bundle, SigstoreFixture.Email, null, null)).ExecuteAsync(Context(), NoServer);

        Assert.AreEqual((CurlExitCode.FailedInit, CurlExitCode.FailedInit), (noIdentity.ExitCode, noRoot.ExitCode));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnUnreadableOrMalformedBundle_FailsWith26BeforeTransferring()
    {
        var missing = await Hook(Path.Combine(directory, "missing.sigstore.json")).ExecuteAsync(Context(), NoServer);
        var malformed = await Hook(Write("bad.sigstore.json", "{\"mediaType\": 1}")).ExecuteAsync(Context(), NoServer);

        Assert.AreEqual((CurlExitCode.ReadError, CurlExitCode.ReadError), (missing.ExitCode, malformed.ExitCode));
    }

    [TestMethod]
    public async Task ExecuteAsync_ARootFileWithoutCertificatesOrMissing_FailsWith26BeforeTransferring()
    {
        var bundle = ValidBundle();

        var empty = await new SigstoreTransferHook(new(bundle, SigstoreFixture.Email, null, Write("empty.pem", "no certificates"))).ExecuteAsync(Context(), NoServer);
        var missing = await new SigstoreTransferHook(new(bundle, SigstoreFixture.Email, null, Path.Combine(directory, "missing.pem"))).ExecuteAsync(Context(), NoServer);
        var corrupt = await new SigstoreTransferHook(new(bundle, SigstoreFixture.Email, null, Write("corrupt.pem", "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n"))).ExecuteAsync(Context(), NoServer);

        Assert.AreEqual(CurlExitCode.ReadError, empty.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, missing.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, corrupt.ExitCode);
        StringAssert.Contains(empty.ErrorMessage, "--sigstore-root");
    }

    private string ValidBundle()
    {
        using var certificate = fulcio.Issue(signer);
        return Write("artifact.sigstore.json", SigstoreFixture.Bundle(certificate, Artifact, SigstoreFixture.Sign(signer, Artifact)));
    }

    private SigstoreTransferHook Hook(string bundlePath, string identity = SigstoreFixture.Email) =>
        new(new(bundlePath, identity, SigstoreFixture.Issuer, RootFile()));

    private string RootFile() => Write("fulcio.pem", SigstoreFixture.RootPem(fulcio.Root));

    private string Write(string name, string text)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static Func<ITransferContext, ValueTask<TransferResult>> Serve(byte[] body) => async context =>
    {
        await context.HeaderOutput!.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));
        await context.Output.WriteAsync(body);
        return TransferResult.Success(body.Length);
    };

    private static ValueTask<TransferResult> NoServer(ITransferContext context) => throw new AssertFailedException("The server was contacted.");

    private static byte[] Bytes(Stream stream) => ((MemoryStream)stream).ToArray();

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("https://example.com/release-1.0.tar.gz"),
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
    };
}
