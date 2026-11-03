using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.AzureStorage.DataLake;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage.DataLake;

/// <summary>curl's <c>abfss://</c> client against the reference DFS endpoint: Path - Create, appends and a flush at the right positions, an empty upload, and a listing over several <c>x-ms-continuation</c> pages.</summary>
[TestClass]
public sealed class AzureDataLakeUploadAndListingCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.dfs.core.windows.net";

    private const int BlockSize = 8 * 1024 * 1024;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    private static string Head(string requestLine) =>
        $"{requestLine} HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n";

    [TestMethod]
    [SpecRequirement("AzureStorage:PathCreate-1")]
    [SpecRequirement("AzureStorage:PathUpdate-1")]
    [SpecRequirement("AzureStorage:PathUpdate-2")]
    public Task Upload_EightMiBAndMore_CreatesAppendsAtEachPositionThenFlushesTheLength() =>
        Known.RunCaseAsync(["AzureStorage:PathCreate-1", "AzureStorage:PathUpdate-1", "AzureStorage:PathUpdate-2"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateFileSystem("fs");
            byte[] content = [.. Enumerable.Range(0, BlockSize + 3).Select(index => (byte)(index % 251))];

            CurlRunResult result = await UploadAsync(endpoint, content, "big.bin");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            string expected =
                Head("PUT /fs/dir/big.bin?resource=file") + "Content-Length: 0\r\n\r\n"
                + Head("PATCH /fs/dir/big.bin?action=append&position=0") + $"Content-Length: {BlockSize}\r\n\r\n" + Encoding.Latin1.GetString(content, 0, BlockSize)
                + Head($"PATCH /fs/dir/big.bin?action=append&position={BlockSize}") + "Content-Length: 3\r\n\r\n" + Encoding.Latin1.GetString(content, BlockSize, 3)
                + Head($"PATCH /fs/dir/big.bin?action=flush&position={BlockSize + 3}") + "Content-Length: 0\r\n\r\n";
            AssertWire(expected, Encoding.Latin1.GetString([.. result.Exchanges.SelectMany(exchange => exchange.ClientBytes)]));
            CollectionAssert.AreEqual(content, endpoint.FileContent("fs", "dir/big.bin"));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:PathUpdate-3")]
    public Task Upload_EmptyFile_CreatesThenFlushesAtPositionZeroWithoutAnAppend() =>
        Known.RunCaseAsync(["AzureStorage:PathUpdate-3"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked);
            endpoint.CreateFileSystem("fs");

            CurlRunResult result = await UploadAsync(endpoint, [], "empty.txt");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            Assert.AreEqual(
                Head("PUT /fs/dir/empty.txt?resource=file") + "Content-Length: 0\r\n\r\n"
                + Head("PATCH /fs/dir/empty.txt?action=flush&position=0") + "Content-Length: 0\r\n\r\n",
                Encoding.Latin1.GetString([.. result.Exchanges.SelectMany(exchange => exchange.ClientBytes)]));
            CollectionAssert.AreEqual(Array.Empty<byte>(), endpoint.FileContent("fs", "dir/empty.txt"));
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:PathList-1")]
    [SpecRequirement("AzureStorage:PathList-2")]
    public Task List_ThreePages_FollowsEachContinuationAndPrintsEveryName() =>
        Known.RunCaseAsync(["AzureStorage:PathList-1", "AzureStorage:PathList-2"], async () =>
        {
            AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked, listPageSize: 2);
            endpoint.CreateFileSystem("fs");
            foreach (string name in new[] { "d/a.txt", "d/b.txt", "d/c/x.txt", "d/e.txt", "d/f.txt", "other.txt" })
            {
                endpoint.StoreFile("fs", name, "x"u8.ToArray());
            }

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "abfss://fs@acct/d/");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("d/a.txt\nd/b.txt\nd/c/\nd/e.txt\nd/f.txt\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            const string list = "GET /fs?resource=filesystem&recursive=false&directory=d";
            Assert.AreEqual(
                Head(list) + "\r\n" + Head(list + "&continuation=d%2Fc") + "\r\n" + Head(list + "&continuation=d%2Ff.txt") + "\r\n",
                Encoding.Latin1.GetString([.. result.Exchanges.SelectMany(exchange => exchange.ClientBytes)]));
        });

    private static async Task<CurlRunResult> UploadAsync(AzureDataLakeReferenceEndpoint endpoint, byte[] content, string name)
    {
        string upload = Path.Combine(Directory.CreateTempSubdirectory().FullName, name);
        await File.WriteAllBytesAsync(upload, content);
        try
        {
            return await RunAsync(endpoint, "--oauth2-bearer", "tok", "-T", upload, "abfss://fs@acct/dir/" + name);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(upload)!, recursive: true);
        }
    }

    // An 8 MiB body makes a whole-string failure message unreadable, so a mismatch names the first differing offset.
    private static void AssertWire(string expected, string actual)
    {
        int length = Math.Min(expected.Length, actual.Length);
        int offset = Enumerable.Range(0, length).FirstOrDefault(index => expected[index] != actual[index], length);
        Assert.IsTrue(
            expected.Length == actual.Length && offset == length,
            $"Wire bytes differ at offset {offset} (expected {expected.Length} bytes, got {actual.Length}): expected \"{Excerpt(expected, offset)}\", got \"{Excerpt(actual, offset)}\".");
    }

    private static string Excerpt(string text, int offset) =>
        text.Substring(Math.Max(0, offset - 40), Math.Min(text.Length, offset + 40) - Math.Max(0, offset - 40));

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
