using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>curl's <c>az://</c> client against the reference Blob endpoint: download, a small upload, an <c>Error</c> body and anonymous access.</summary>
[TestClass]
public sealed class AzureBlobTransferCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.blob.core.windows.net";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    [SpecRequirement("AzureStorage:GetBlob-1")]
    [SpecRequirement("AzureStorage:Versioning-1")]
    [SpecRequirement("AzureStorage:AuthorizeOAuth-1")]
    [SpecRequirement("AzureStorage:RFC9112-7.1-1")]
    public Task Download_BearerToken_GetsTheBlobWithVersionDateAndAuthorization() =>
        Known.RunCaseAsync(["AzureStorage:GetBlob-1", "AzureStorage:Versioning-1", "AzureStorage:AuthorizeOAuth-1", "AzureStorage:RFC9112-7.1-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked);
            endpoint.CreateContainer("c");
            endpoint.StoreBlob("c", "hello.txt", "hello, blob\n"u8.ToArray());

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "az://acct/c/hello.txt");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("hello, blob\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                $"GET /c/hello.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:PutBlob-1")]
    [SpecRequirement("AzureStorage:PutBlob-2")]
    public Task Upload_SmallFile_SendsOnePutBlobOfABlockBlob() =>
        Known.RunCaseAsync(["AzureStorage:PutBlob-1", "AzureStorage:PutBlob-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");
            string upload = Path.Combine(Directory.CreateTempSubdirectory().FullName, "up.txt");
            await File.WriteAllTextAsync(upload, "hello");
            try
            {
                CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "-T", upload, "az://acct/c/up.txt");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
                Assert.AreEqual(
                    $"PUT /c/up.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\nx-ms-blob-type: BlockBlob\r\nContent-Length: 5\r\n\r\nhello",
                    Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
                CollectionAssert.AreEqual("hello"u8.ToArray(), endpoint.BlobContent("c", "up.txt"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(upload)!, recursive: true);
            }
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:ErrorCodes-1")]
    public Task Download_MissingBlob_ReadsTheErrorBodyAndExits22() =>
        Known.RunCaseAsync(["AzureStorage:ErrorCodes-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "az://acct/c/missing.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(
                "curl: (22) Azure Storage returned error 404: BlobNotFound: The specified blob does not exist." + Environment.NewLine,
                Encoding.Latin1.GetString(result.StandardError));
            StringAssert.StartsWith(Encoding.Latin1.GetString(result.Exchanges.Single().ServerBytes), "HTTP/1.1 404 The specified blob does not exist.\r\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:AnonymousAccess-1")]
    public Task Download_NoCredential_SendsNoAuthorizationAndReadsAPublicContainer() =>
        Known.RunCaseAsync(["AzureStorage:AnonymousAccess-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("public", anonymousRead: true);
            endpoint.StoreBlob("public", "a.txt", "open\n"u8.ToArray());

            CurlRunResult result = await RunAsync(endpoint, "az://acct/public/a.txt");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("open\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(
                $"GET /public/a.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    private static Task<CurlRunResult> RunAsync(AzureBlobReferenceEndpoint endpoint, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(
            Curl,
            [ReferenceRoute.Tls(HttpsPort, endpoint, ReferenceCertificate.CreateSelfSigned(Host))],
            ["-sS", "-k", .. arguments],
            timeProvider: new FixedClock());

    // Only the wall clock is fixed, so x-ms-date is known; every timer still runs on the system's.
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
