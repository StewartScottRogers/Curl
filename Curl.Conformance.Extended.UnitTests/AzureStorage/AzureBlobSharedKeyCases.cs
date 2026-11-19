using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>curl's <c>az://</c> client against the reference Blob endpoint with <c>-u account:key</c>: the request is signed with Shared Key and the key is never shown.</summary>
[TestClass]
public sealed class AzureBlobSharedKeyCases
{
    private const string Host = "acct.blob.core.windows.net";

    // Azurite's well-known development key.
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    [SpecRequirement("AzureStorage:AuthorizeSharedKey-1")]
    public Task Download_AccountAndKey_SendsAnAuthorizationSharedKeySignatureOverTheCanonicalRequest() =>
        Known.RunCaseAsync(["AzureStorage:AuthorizeSharedKey-1"], async () =>
        {
            CurlRunResult result = await RunAsync("-sS");

            string stringToSign = $"GET\n\n\n\n\n\n\n\n\n\n\n\nx-ms-date:{Date}\nx-ms-version:2025-01-05\n/acct/c/x.txt";
            string signature = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(Key), Encoding.UTF8.GetBytes(stringToSign)));
            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("x\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(
                $"GET /c/x.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: SharedKey acct:{signature}\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    public Task Download_AccountAndKeyVerbose_NeverShowsTheKey() =>
        Known.RunCaseAsync([], async () =>
        {
            CurlRunResult result = await RunAsync("-sS", "-v");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            StringAssert.Contains(Encoding.Latin1.GetString(result.StandardError), "Authorization: SharedKey acct:");
            Assert.IsFalse(Encoding.Latin1.GetString(result.StandardError).Contains(Key, StringComparison.Ordinal));
            Assert.IsFalse(Encoding.Latin1.GetString(result.StandardError).Contains("Eby8vdM02xNOcqFlqUw", StringComparison.Ordinal));
        });

    private static Task<CurlRunResult> RunAsync(params string[] options)
    {
        AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
        endpoint.CreateContainer("c");
        endpoint.StoreBlob("c", "x.txt", "x\n"u8.ToArray());
        return ExtendedConformanceHarness.RunAsync(
            Curl,
            [ReferenceRoute.Tls(443, endpoint, ReferenceCertificate.CreateSelfSigned(Host))],
            ["-k", .. options, "-u", "acct:" + Key, "az://acct/c/x.txt"],
            null,
            new FixedClock());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
