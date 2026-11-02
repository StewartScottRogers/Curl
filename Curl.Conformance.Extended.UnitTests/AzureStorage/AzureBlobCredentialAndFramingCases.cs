using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>
/// curl's <c>az://</c> client against the reference Blob endpoint: each credential source in order of precedence
/// (<c>--oauth2-bearer</c>, <c>AZURE_STORAGE_BEARER_TOKEN</c>, <c>AZURE_STORAGE_SAS_TOKEN</c>) and responses framed
/// by chunks and by the close.
/// </summary>
[TestClass]
public sealed class AzureBlobCredentialAndFramingCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.blob.core.windows.net";

    private const string Sas = "sv=2025-01-05&sp=r&sig=c2ln";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    [SpecRequirement("AzureStorage:CredentialPrecedence-1")]
    public Task Download_OAuth2BearerAndBothVariables_SendsTheOptionsToken() =>
        Known.RunCaseAsync(["AzureStorage:CredentialPrecedence-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.ContentLength, "x\n");

            CurlRunResult result = await RunAsync(
                endpoint,
                new Dictionary<string, string> { ["AZURE_STORAGE_BEARER_TOKEN"] = "envtok", ["AZURE_STORAGE_SAS_TOKEN"] = Sas },
                "--oauth2-bearer",
                "opttok",
                "az://acct/c/x.txt");

            AssertDownloaded(result, "x\n", $"GET /c/x.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer opttok\r\n\r\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:CredentialPrecedence-2")]
    public Task Download_BearerVariableAndSasVariable_SendsTheBearerVariable() =>
        Known.RunCaseAsync(["AzureStorage:CredentialPrecedence-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.ContentLength, "x\n");

            CurlRunResult result = await RunAsync(
                endpoint,
                new Dictionary<string, string> { ["AZURE_STORAGE_BEARER_TOKEN"] = "envtok", ["AZURE_STORAGE_SAS_TOKEN"] = Sas },
                "az://acct/c/x.txt");

            AssertDownloaded(result, "x\n", $"GET /c/x.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer envtok\r\n\r\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:CredentialPrecedence-3")]
    [SpecRequirement("AzureStorage:AuthorizeSas-1")]
    public Task Download_SasVariableAlone_AppendsTheTokenToTheQueryAndSendsNoAuthorization() =>
        Known.RunCaseAsync(["AzureStorage:CredentialPrecedence-3", "AzureStorage:AuthorizeSas-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.ContentLength, "x\n");

            CurlRunResult result = await RunAsync(
                endpoint,
                new Dictionary<string, string> { ["AZURE_STORAGE_SAS_TOKEN"] = "?" + Sas },
                "az://acct/c/x.txt");

            AssertDownloaded(result, "x\n", $"GET /c/x.txt?{Sas} HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\n\r\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:RFC9112-7.1-2")]
    public Task Download_ChunkedEmptyBlob_WritesNothingAndExits0() =>
        Known.RunCaseAsync(["AzureStorage:RFC9112-7.1-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.Chunked, string.Empty);

            CurlRunResult result = await RunAsync(endpoint, null, "--oauth2-bearer", "tok", "az://acct/c/x.txt");

            AssertDownloaded(result, string.Empty, $"GET /c/x.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n");
            StringAssert.EndsWith(Encoding.Latin1.GetString(result.Exchanges.Single().ServerBytes), "Transfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:RFC9112-6.3-1")]
    public Task Download_CloseDelimitedBody_ReadsToTheClose() =>
        Known.RunCaseAsync(["AzureStorage:RFC9112-6.3-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.Close, "until the close\n");

            CurlRunResult result = await RunAsync(endpoint, null, "--oauth2-bearer", "tok", "az://acct/c/x.txt");

            AssertDownloaded(result, "until the close\n", $"GET /c/x.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n");
            StringAssert.EndsWith(Encoding.Latin1.GetString(result.Exchanges.Single().ServerBytes), "Connection: close\r\n\r\nuntil the close\n");
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:RFC9112-6.3-2")]
    public Task Download_CloseDelimitedErrorBody_ReadsTheErrorAndExits22() =>
        Known.RunCaseAsync(["AzureStorage:RFC9112-6.3-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.Close, "x\n");

            CurlRunResult result = await RunAsync(endpoint, null, "--oauth2-bearer", "tok", "az://acct/c/missing.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(
                "curl: (22) Azure Storage returned error 404: BlobNotFound: The specified blob does not exist." + Environment.NewLine,
                Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                $"GET /c/missing.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    private static AzureBlobReferenceEndpoint Endpoint(AzureStorageResponseFraming framing, string content)
    {
        AzureBlobReferenceEndpoint endpoint = new(framing);
        endpoint.CreateContainer("c");
        endpoint.StoreBlob("c", "x.txt", Encoding.Latin1.GetBytes(content));
        return endpoint;
    }

    private static void AssertDownloaded(CurlRunResult result, string standardOutput, string request)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(standardOutput, Encoding.Latin1.GetString(result.StandardOutput));
        Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
        Assert.AreEqual(request, Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
    }

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
