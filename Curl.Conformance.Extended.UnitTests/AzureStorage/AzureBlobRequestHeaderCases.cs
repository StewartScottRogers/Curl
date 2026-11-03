using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>curl's <c>az://</c> client against the reference Blob endpoint: the headers every operation requires and a SAS on an operation with its own query.</summary>
[TestClass]
public sealed class AzureBlobRequestHeaderCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.blob.core.windows.net";

    private const string Sas = "sv=2025-01-05&sr=c&sp=rl&sig=c2ln%3D";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    [TestMethod]
    [SpecRequirement("AzureStorage:RFC9112-3.2-1")]
    [SpecRequirement("AzureStorage:RequestHeaders-1")]
    [SpecRequirement("AzureStorage:AuthorizeSas-2")]
    public Task List_SasVariable_SendsHostVersionDateAndTheTokenAfterTheListQuery() =>
        Known.RunCaseAsync(["AzureStorage:RFC9112-3.2-1", "AzureStorage:RequestHeaders-1", "AzureStorage:AuthorizeSas-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");
            endpoint.StoreBlob("c", "a.txt", "x"u8.ToArray());

            CurlRunResult result = await RunAsync(endpoint, new Dictionary<string, string> { ["AZURE_STORAGE_SAS_TOKEN"] = Sas }, "az://acct/c/");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("a.txt\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                $"GET /c?restype=container&comp=list&delimiter=%2F&prefix=&{Sas} HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: Mon, 02 Nov 2026 12:00:00 GMT\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:PutBlob-3")]
    public Task Upload_EmptyFile_SendsOnePutBlobWithContentLengthZero() =>
        Known.RunCaseAsync(["AzureStorage:PutBlob-3"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");
            string upload = Path.Combine(Directory.CreateTempSubdirectory().FullName, "empty.txt");
            await File.WriteAllBytesAsync(upload, []);
            try
            {
                CurlRunResult result = await RunAsync(endpoint, null, "--oauth2-bearer", "tok", "-T", upload, "az://acct/c/empty.txt");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
                Assert.AreEqual(
                    $"PUT /c/empty.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\nx-ms-blob-type: BlockBlob\r\nContent-Length: 0\r\n\r\n",
                    Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
                CollectionAssert.AreEqual(Array.Empty<byte>(), endpoint.BlobContent("c", "empty.txt"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(upload)!, recursive: true);
            }
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:PutBlockList-2")]
    [SpecRequirement("AzureStorage:PutBlock-3")]
    [SpecRequirement("AzureStorage:RFC9112-6.1-1")]
    [SpecRequirement("AzureStorage:AuthorizeOAuth-2")]
    public Task Upload_EightMiBAndMoreWithBearer_FramesEveryBodyByContentLengthOverTls() =>
        Known.RunCaseAsync(["AzureStorage:PutBlockList-2", "AzureStorage:PutBlock-3", "AzureStorage:RFC9112-6.1-1", "AzureStorage:AuthorizeOAuth-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");
            string upload = Path.Combine(Directory.CreateTempSubdirectory().FullName, "big.bin");
            await File.WriteAllBytesAsync(upload, new byte[(8 * 1024 * 1024) + 1]);
            try
            {
                CurlRunResult result = await RunAsync(endpoint, null, "--oauth2-bearer", "tok", "-T", upload, "az://acct/c/big.bin");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                const string blockList = "<BlockList><Latest>MDAwMDAw</Latest><Latest>MDAwMDAx</Latest></BlockList>";
                string[] heads = [.. result.Exchanges.Select(exchange => Encoding.Latin1.GetString(exchange.ClientBytes).Split("\r\n\r\n")[0])];
                Assert.HasCount(3, heads);
                foreach (string head in heads)
                {
                    StringAssert.Contains(head, "\r\nAuthorization: Bearer tok\r\n");
                    StringAssert.Contains(head, "\r\nContent-Length: ");
                    Assert.DoesNotContain("Transfer-Encoding", head, StringComparison.OrdinalIgnoreCase);
                }

                StringAssert.EndsWith(heads[2], $"\r\nContent-Length: {blockList.Length}");
                StringAssert.EndsWith(Encoding.Latin1.GetString(result.Exchanges[2].ClientBytes), blockList);
                Assert.IsTrue(endpoint.CommittedBlockIds("c", "big.bin")!.All(id => Convert.FromBase64String(id).Length <= 64));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(upload)!, recursive: true);
            }
        });

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    private static Task<CurlRunResult> RunAsync(AzureBlobReferenceEndpoint endpoint, IReadOnlyDictionary<string, string>? environment, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(
            Curl,
            [ReferenceRoute.Tls(HttpsPort, endpoint, ReferenceCertificate.CreateSelfSigned(Host))],
            ["-sS", "-k", .. arguments],
            environment,
            new FixedClock());

    // Only the wall clock is fixed, so x-ms-date is known; every timer still runs on the system's.
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
