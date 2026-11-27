using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class DpopWithResumableUploadOrderTests
{
    [TestMethod]
    public void Catalogue_DpopWithResumableUpload_TheUploadRunsOutsideTheDpopHookSoEachRequestIsSigned()
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { ["dpop"] = "k.pem", ["resumable-upload"] = "x" });

        var hooks = TransferHooks.ActivatedBy(values, ExtensionTransferHookCatalogue.Entries).Hooks;

        var names = hooks.Select(hook => hook.GetType().Name).ToList();
        CollectionAssert.AreEqual(new[] { "ResumableUploadTransferHook", "DpopTransferHook" }, names, string.Join(",", names));
    }

    [TestMethod]
    public async Task Catalogue_DpopWithATusUpload_SignsEachRequestForItsOwnMethodAndTarget_Rfc9449Section4Point2()
    {
        var directory = Directory.CreateTempSubdirectory("curl-dpop-").FullName;
        try
        {
            var keyFile = Path.Combine(directory, "key.pem");
            using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                await File.WriteAllTextAsync(keyFile, ecdsa.ExportECPrivateKeyPem());
            }

            var values = new ExtensionOptionValues(new Dictionary<string, string> { ["dpop"] = keyFile, ["resumable-upload"] = "x" });
            var server = new TusServer();
            var context = new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:27020/tus"),
                Output = new MemoryStream(),
                Upload = new MemoryStream("upload"u8.ToArray()),
                Http = new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = "DTOK" },
            };

            var result = await TransferHooks.ActivatedBy(values, ExtensionTransferHookCatalogue.Entries).Around(server).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
            var proofs = server.Requests.Select(ClaimsOf).ToList();
            CollectionAssert.AreEqual(new[] { "OPTIONS", "POST", "PATCH" }, proofs.Select(proof => proof.GetProperty("htm").GetString()).ToArray());
            CollectionAssert.AreEqual(
                new[] { "http://127.0.0.1:27020/tus", "http://127.0.0.1:27020/tus", "http://127.0.0.1:27020/files/1" },
                proofs.Select(proof => proof.GetProperty("htu").GetString()).ToArray());
            Assert.AreEqual(3, proofs.Select(proof => proof.GetProperty("jti").GetString()).Distinct().Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonElement ClaimsOf(IReadOnlyList<string> headers)
    {
        var proof = headers.Single(header => header.StartsWith("DPoP: ", StringComparison.Ordinal))["DPoP: ".Length..];
        return JsonElement.Parse(Base64Url.DecodeFromChars(proof.Split('.')[1]));
    }

    /// <summary>Stands in for curl's HTTP handler in front of a tus 1.0 server, recording each request's headers.</summary>
    private sealed class TusServer : IProtocolHandler
    {
        public List<IReadOnlyList<string>> Requests { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Requests.Add(context.Http!.Headers);
            var report = Requests.Count switch
            {
                1 => new TransferReport { ResponseCode = 204, ResponseHeaders = [new("Tus-Version", "1.0.0"), new("Tus-Extension", "creation")] },
                2 => new TransferReport { ResponseCode = 201, ResponseHeaders = [new("Location", "/files/1")] },
                _ => new TransferReport { ResponseCode = 204, ResponseHeaders = [new("Upload-Offset", "6")] },
            };
            return ValueTask.FromResult(TransferResult.Success(0) with { Report = report });
        }
    }
}
