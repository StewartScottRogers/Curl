using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>curl's <c>az://</c> client against the reference Blob endpoint: an upload of 8 MiB or more as Put Block calls and a Put Block List, and a listing over several <c>NextMarker</c> pages.</summary>
[TestClass]
public sealed class AzureBlobBlockUploadAndListingCases
{
    private const int HttpsPort = 443;

    private const string Host = "acct.blob.core.windows.net";

    private const int BlockSize = 8 * 1024 * 1024;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static string Date => Now.ToString("R", CultureInfo.InvariantCulture);

    private static string Head(string requestLine) =>
        $"{requestLine} HTTP/1.1\r\nHost: {Host}\r\nx-ms-version: 2025-01-05\r\nx-ms-date: {Date}\r\nAuthorization: Bearer tok\r\n";

    [TestMethod]
    [SpecRequirement("AzureStorage:PutBlock-1")]
    [SpecRequirement("AzureStorage:PutBlock-2")]
    [SpecRequirement("AzureStorage:PutBlockList-1")]
    public Task Upload_EightMiBAndMore_SendsEqualLengthBlocksThenTheBlockListInOrder() =>
        Known.RunCaseAsync(["AzureStorage:PutBlock-1", "AzureStorage:PutBlock-2", "AzureStorage:PutBlockList-1"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength);
            endpoint.CreateContainer("c");
            byte[] content = [.. Enumerable.Range(0, BlockSize + 3).Select(index => (byte)(index % 251))];
            string upload = Path.Combine(Directory.CreateTempSubdirectory().FullName, "big.bin");
            await File.WriteAllBytesAsync(upload, content);
            try
            {
                CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "-T", upload, "az://acct/c/big.bin");

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardOutput));
                Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
                const string blockList = "<BlockList><Latest>MDAwMDAw</Latest><Latest>MDAwMDAx</Latest></BlockList>";
                string expected =
                    Head("PUT /c/big.bin?comp=block&blockid=MDAwMDAw") + $"Content-Length: {BlockSize}\r\n\r\n" + Encoding.Latin1.GetString(content, 0, BlockSize)
                    + Head("PUT /c/big.bin?comp=block&blockid=MDAwMDAx") + "Content-Length: 3\r\n\r\n" + Encoding.Latin1.GetString(content, BlockSize, 3)
                    + Head("PUT /c/big.bin?comp=blocklist") + $"Content-Length: {blockList.Length}\r\n\r\n" + blockList;
                AssertWire(expected, Encoding.Latin1.GetString([.. result.Exchanges.SelectMany(exchange => exchange.ClientBytes)]));
                CollectionAssert.AreEqual(new[] { "MDAwMDAw", "MDAwMDAx" }, endpoint.CommittedBlockIds("c", "big.bin")!.ToArray());
                CollectionAssert.AreEqual(content, endpoint.BlobContent("c", "big.bin"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(upload)!, recursive: true);
            }
        });

    [TestMethod]
    [SpecRequirement("AzureStorage:ListBlobs-1")]
    [SpecRequirement("AzureStorage:ListBlobs-2")]
    public Task List_ThreePages_FollowsEachNextMarkerAndPrintsEveryName() =>
        Known.RunCaseAsync(["AzureStorage:ListBlobs-1", "AzureStorage:ListBlobs-2"], async () =>
        {
            AzureBlobReferenceEndpoint endpoint = new(AzureStorageResponseFraming.Chunked, listPageSize: 2);
            endpoint.CreateContainer("c");
            foreach (string name in new[] { "a.txt", "b.txt", "c.txt", "d.txt", "e.txt" })
            {
                endpoint.StoreBlob("c", name, "x"u8.ToArray());
            }

            CurlRunResult result = await RunAsync(endpoint, "--oauth2-bearer", "tok", "az://acct/c/");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("a.txt\nb.txt\nc.txt\nd.txt\ne.txt\n", Encoding.Latin1.GetString(result.StandardOutput));
            Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
            const string list = "GET /c?restype=container&comp=list&delimiter=%2F&prefix=";
            Assert.AreEqual(
                Head(list) + "\r\n" + Head(list + "&marker=c.txt") + "\r\n" + Head(list + "&marker=e.txt") + "\r\n",
                Encoding.Latin1.GetString([.. result.Exchanges.SelectMany(exchange => exchange.ClientBytes)]));
        });

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
