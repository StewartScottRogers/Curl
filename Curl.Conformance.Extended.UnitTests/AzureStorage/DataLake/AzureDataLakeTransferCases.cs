using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.AzureStorage.DataLake;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage.DataLake;

/// <summary>curl's <c>abfs://</c> and <c>abfss://</c> client against the reference DFS endpoint: Path - Read through a bare account name and a full host, and a JSON error body.</summary>
[TestClass]
public sealed class AzureDataLakeTransferCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.dfs.core.windows.net";

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    [SpecRequirement("AzureStorage:PathRead-1")]
    [SpecRequirement("AzureStorage:DfsUri-2")]
    public Task Read_AbfssBareAccount_GetsThePathFromTheAccountsDfsHost() =>
        Known.RunCaseAsync(["AzureStorage:PathRead-1", "AzureStorage:DfsUri-2"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked);
            endpoint.CreateFileSystem("fs");
            endpoint.StoreFile("fs", "dir/hello.txt", "hello, lake\n"u8.ToArray());

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "abfss://fs@acct/dir/hello.txt");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("hello, lake\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                $"GET /fs/dir/hello.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:DfsUri-1")]
    public Task Read_AbfsFullDfsHost_GetsThePathFromThatHost() =>
        Known.RunCaseAsync(["AzureStorage:DfsUri-1"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateFileSystem("fs");
            endpoint.StoreFile("fs", "a.txt", "abc"u8.ToArray());

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", $"abfs://fs@{Host}/a.txt");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("abc", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                $"GET /fs/a.txt HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n\r\n",
                Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:DfsErrors-1")]
    public Task Read_MissingPath_ReadsTheJsonErrorBodyAndExits22() =>
        Known.RunCaseAsync(["AzureStorage:DfsErrors-1"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateFileSystem("fs");

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "abfss://fs@acct/missing.txt");

            Assert.AreEqual(22, result.ExitCode);
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(
                "curl: (22) Azure Storage returned error 404: PathNotFound: The specified path does not exist." + Environment.NewLine,
                Encoding.Latin1.GetString(result.StandardError));
            StringAssert.Contains(
                Encoding.Latin1.GetString(result.Exchanges.Single().ServerBytes),
                "{\"error\":{\"code\":\"PathNotFound\",\"message\":\"The specified path does not exist.\"}}");
        });

    private static Task<CurlRunResult> RunAsync(AzureDataLakeReferenceEndpoint endpoint, params string[] arguments) =>
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
