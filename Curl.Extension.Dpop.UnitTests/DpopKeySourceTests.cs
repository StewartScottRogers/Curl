using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Dpop.UnitTests;

/// <summary>
/// Pins the key <c>--dpop</c>'s hook lends the other hooks (RFC 9449 §5, RFC 7638) and that it signs the request carrying a
/// token one of them added (§7.1).
/// </summary>
[TestClass]
public sealed class DpopKeySourceTests
{
    private string _directory = null!;
    private string _keyFile = null!;

    [TestInitialize]
    public void WriteKey()
    {
        _directory = Directory.CreateTempSubdirectory("dpop-source-").FullName;
        _keyFile = Path.Combine(_directory, "key.pem");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(_keyFile, ecdsa.ExportECPrivateKeyPem());
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task ReadDpopKeyAsync_AMissingKeyFile_GivesNoKey()
    {
        IDpopKeySource source = new DpopTransferHook(Path.Combine(_directory, "missing.pem"));

        Assert.IsNull(await source.ReadDpopKeyAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ProofForPost_AnEndpointWithAQuery_ProvesThePostWithoutAnAth_Rfc9449Section5()
    {
        var key = await new DpopTransferHook(_keyFile).ReadDpopKeyAsync(CancellationToken.None);

        var proof = key!.ProofForPost(new Uri("https://Server.Example.COM:8443/token?x=1#f"), DateTimeOffset.UnixEpoch, "n1");

        var payload = DpopProofTests.Decode(proof.Split('.')[1]);
        Assert.AreEqual("POST", payload.GetProperty("htm").GetString());
        Assert.AreEqual("https://server.example.com:8443/token", payload.GetProperty("htu").GetString());
        Assert.AreEqual("n1", payload.GetProperty("nonce").GetString());
        Assert.IsFalse(payload.TryGetProperty("ath", out _));
    }

    [TestMethod]
    public async Task Thumbprint_IsTheSha256OfTheProofsJwk_Rfc7638Section3()
    {
        var key = await new DpopTransferHook(_keyFile).ReadDpopKeyAsync(CancellationToken.None);

        var proof = key!.ProofForPost(new Uri("https://a.example/token"), DateTimeOffset.UnixEpoch, null);

        var jwk = DpopProofTests.Decode(proof.Split('.')[0]).GetProperty("jwk").GetRawText();
        Assert.AreEqual(Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(jwk))), key.Thumbprint);
    }

    [TestMethod]
    public async Task ExecuteAsync_ADpopAuthorizationHeaderBesideABearerToken_BindsTheHeadersTokenAndAddsNoSecondOne()
    {
        IReadOnlyList<string>? headers = null;
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("https://a.example/r"),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { BearerToken = "own", Headers = ["Authorization: DPoP fetched"] },
        };

        await ((ITransferHook)new DpopTransferHook(_keyFile)).ExecuteAsync(context, inner =>
        {
            headers = inner.Http!.Headers;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.HasCount(2, headers!);
        var payload = DpopProofTests.Decode(headers![1].Split('.')[1]);
        Assert.AreEqual(DpopProof.AccessTokenHashOf("fetched"), payload.GetProperty("ath").GetString());
    }
}
