using System.Security.Cryptography;
using System.Text;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.CompressionDictionary.UnitTests.Brotli;
using Curl.Extension.CompressionDictionary.UnitTests.Zstd;

namespace Curl.Extension.CompressionDictionary.UnitTests;

[TestClass]
public sealed class CompressionDictionaryTransferHookTests
{
    private const string DictionaryUrl = "https://example.com/app/dictionary.txt";
    private const string ResourceUrl = "https://example.com/app/main.js";
    private const string UseAsDictionaryField = "Use-As-Dictionary: match=\"/app/*\", id=\"v1\"";

    private static readonly byte[] BrotliMagic = [0xFF, 0x44, 0x43, 0x42];
    private static readonly byte[] ZstandardMagic = [0x5E, 0x2A, 0x4D, 0x18, 0x20, 0x00, 0x00, 0x00];

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory() => _directory = Path.Combine(Path.GetTempPath(), "dictionaries-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
        else if (File.Exists(_directory))
        {
            File.Delete(_directory);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDictionaryStored_SendsTheRequestUnchangedAndWritesTheBody()
    {
        var origin = Origin(200, "plain body");
        var context = Context(ResourceUrl);

        var result = await Hook().ExecuteAsync(context, origin.Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("plain body", Text(context.Output));
        Assert.IsEmpty(origin.Sent!.Http!.Headers);
        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferItTakesNoPartIn_IsPassedOnAsGiven_Rfc9842Section2_2()
    {
        foreach (var context in new[] { Context("ftp://example.com/f", withHttp: false), Context("ftp://example.com/f"), Context(ResourceUrl, noBody: true), Context(ResourceUrl, range: true), Context(ResourceUrl, resumeFrom: 5) })
        {
            var origin = Origin(200, "body", UseAsDictionaryField);

            await Hook().ExecuteAsync(context, origin.Invoke);

            Assert.AreSame(context, origin.Sent);
        }

        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task ExecuteAsync_AResponseWithUseAsDictionary_IsStoredAndAdvertisedOnAMatchingRequest_Rfc9842Sections2_1To2_3()
    {
        await StoreDictionaryAsync(Encoding.ASCII.GetBytes("dictionary bytes"));
        var origin = Origin(200, "next");

        await Hook().ExecuteAsync(Context(ResourceUrl), origin.Invoke);

        CollectionAssert.AreEqual(
            new[]
            {
                "Available-Dictionary: :" + Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes("dictionary bytes"))) + ":",
                "Dictionary-ID: \"v1\"",
                "Accept-Encoding: dcb, dcz",
            },
            origin.Sent!.Http!.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AUrlWithAPortAndQuery_StoresAndMatchesOnTheSameOrigin_Rfc9842Section2_2_2()
    {
        var origin = Origin(200, "next");
        await Hook().ExecuteAsync(Context("https://example.com:8443/app/d.txt?v=1"), Origin(200, "dictionary", UseAsDictionaryField).Invoke);

        await Hook().ExecuteAsync(Context("https://example.com:8443/app/main.js?v=2"), origin.Invoke);

        Assert.HasCount(3, origin.Sent!.Http!.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_ADictionaryWithoutAnId_SendsNoDictionaryId_Rfc9842Section2_3()
    {
        await StoreDictionaryAsync([1, 2, 3], "Use-As-Dictionary: match=\"/app/*\"");
        var origin = Origin(200, "next");

        await Hook().ExecuteAsync(Context(ResourceUrl), origin.Invoke);

        Assert.HasCount(2, origin.Sent!.Http!.Headers);
        Assert.DoesNotContain("Dictionary-ID", string.Join("\n", origin.Sent.Http.Headers));
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequestTheDictionaryDoesNotMatch_AdvertisesNothing_Rfc9842Section2_2_2()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        var origin = Origin(200, "next");

        await Hook().ExecuteAsync(Context("https://example.com/other/main.js"), origin.Invoke);

        Assert.IsEmpty(origin.Sent!.Http!.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedOrAnAcceptEncodingHeaderGiven_AdvertisesNothing()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        foreach (var http in new[] { new HttpRequestOptions { Compressed = true }, new HttpRequestOptions { Headers = ["accept-encoding: gzip"] } })
        {
            var origin = Origin(200, "next");

            await Hook().ExecuteAsync(Context(ResourceUrl, http), origin.Invoke);

            Assert.AreEqual(http.Headers.Count, origin.Sent!.Http!.Headers.Count);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TheDictionaryFileIsGone_AdvertisesNothing()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        foreach (var file in Directory.EnumerateFiles(_directory, "*.dictionary"))
        {
            File.Delete(file);
        }

        var origin = Origin(200, "next");

        await Hook().ExecuteAsync(Context(ResourceUrl), origin.Invoke);

        Assert.IsEmpty(origin.Sent!.Http!.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_ADcbResponse_IsDecodedWithTheDictionaryAsABrotliPrefix_Rfc9842Section4()
    {
        await StoreDictionaryAsync(BrotliRecordedStreams.Dictionary);
        var context = Context(ResourceUrl);
        var body = Framed(BrotliMagic, BrotliRecordedStreams.Dictionary, BrotliRecordedStreams.WithDictionaryQuality5Window10);

        var result = await Hook().ExecuteAsync(context, Origin(200, body, "Content-Encoding: dcb").Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(BrotliRecordedStreams.WithDictionarySha256, Convert.ToHexStringLower(SHA256.HashData(((MemoryStream)context.Output).ToArray())));
    }

    [TestMethod]
    public async Task ExecuteAsync_ADczResponse_IsDecodedWithTheDictionaryAsARawZstandardDictionary_Rfc9842Section5()
    {
        await StoreDictionaryAsync(DictionaryZstandardDecompressorTests.Dictionary);
        var context = Context(ResourceUrl);
        var body = Framed(ZstandardMagic, DictionaryZstandardDecompressorTests.Dictionary, Convert.FromHexString(DictionaryZstandardDecompressorTests.UsingDictionaryFrame));

        var result = await Hook().ExecuteAsync(context, Origin(200, body, "Content-Encoding: DCZ").Invoke);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(DictionaryZstandardDecompressorTests.Content, ((MemoryStream)context.Output).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AStreamHeaderNamingAnotherDictionary_FailsWithBadContentEncoding_Rfc9842Section4()
    {
        await StoreDictionaryAsync(BrotliRecordedStreams.Dictionary);
        var context = Context(ResourceUrl);
        var body = Framed(BrotliMagic, [9, 9, 9], BrotliRecordedStreams.WithDictionaryQuality5Window10);

        var result = await Hook().ExecuteAsync(context, Origin(200, body, "Content-Encoding: dcb").Invoke);

        Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode);
        StringAssert.Contains(result.ErrorMessage, "WrongDictionary");
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_CorruptCompressedData_FailsWithBadContentEncoding_Rfc9842Sections4And5()
    {
        foreach (var (magic, coding) in new[] { (BrotliMagic, "dcb"), (ZstandardMagic, "dcz") })
        {
            await StoreDictionaryAsync(BrotliRecordedStreams.Dictionary);
            var body = Framed(magic, BrotliRecordedStreams.Dictionary, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

            var result = await Hook().ExecuteAsync(Context(ResourceUrl), Origin(200, body, "Content-Encoding: " + coding).Invoke);

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, coding);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ADcbResponseWithNoDictionaryAdvertisedOrAFailedTransfer_IsWrittenAsReceived()
    {
        var context = Context(ResourceUrl);

        await Hook().ExecuteAsync(context, Origin(200, "raw", "Content-Encoding: dcb").Invoke);

        Assert.AreEqual("raw", Text(context.Output));
        await StoreDictionaryAsync([1, 2, 3]);
        var failed = Context(ResourceUrl);
        var result = await Hook().ExecuteAsync(failed, Origin(200, "partial", "Content-Encoding: dcb").Failing);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("partial", Text(failed.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_AResponseThatIsNotAUsableDictionary_IsNotStored_Rfc9842Section2_1()
    {
        await Hook().ExecuteAsync(Context(DictionaryUrl), Origin(404, "missing", UseAsDictionaryField).Invoke);
        await Hook().ExecuteAsync(Context(DictionaryUrl), Origin(200, "bad", "Use-As-Dictionary: id=\"no match\"").Invoke);
        await Hook().ExecuteAsync(Context(DictionaryUrl), Origin(200, "failed", UseAsDictionaryField).Failing);
        await Hook().ExecuteAsync(Context(DictionaryUrl), Origin(200, "no report", UseAsDictionaryField).WithoutReport);

        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task ExecuteAsync_ADirectoryThatCannotBeWritten_LeavesTheTransferAsItWas()
    {
        await File.WriteAllTextAsync(_directory, "a file, not a directory");
        var context = Context(DictionaryUrl);

        var result = await Hook().ExecuteAsync(context, Origin(200, "dictionary", UseAsDictionaryField).Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("dictionary", Text(context.Output));
    }

    [TestMethod]
    public void CodingOf_ContentEncodingValues_NamesOnlyTheDictionaryCodings_Rfc9842Section6()
    {
        Assert.AreEqual(DictionaryContentEncoding.DictionaryCompressedBrotli, DictionaryCompressedBody.CodingOf(" dcb "));
        Assert.AreEqual(DictionaryContentEncoding.DictionaryCompressedZstandard, DictionaryCompressedBody.CodingOf("Dcz"));
        Assert.IsNull(DictionaryCompressedBody.CodingOf("gzip"));
        Assert.IsNull(DictionaryCompressedBody.CodingOf(null));
    }

    private static byte[] Framed(byte[] magic, byte[] dictionary, byte[] compressed) => [.. magic, .. SHA256.HashData(dictionary), .. compressed];

    private static string Text(Stream stream) => Encoding.Latin1.GetString(((MemoryStream)stream).ToArray());

    private static OriginServer Origin(int code, string body, params string[] fields) => new(code, Encoding.Latin1.GetBytes(body), fields);

    private static OriginServer Origin(int code, byte[] body, params string[] fields) => new(code, body, fields);

    private static TransferContext Context(string url, HttpRequestOptions? http = null, bool noBody = false, bool range = false, long? resumeFrom = null, bool withHttp = true) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        Http = withHttp ? http ?? new HttpRequestOptions() : null,
        NoBody = noBody,
        Range = range ? ByteRange.Bounded(0, 9) : null,
        ResumeFrom = resumeFrom,
    };

    private ITransferHook Hook() => CompressionDictionaryExtension.HookFor(new(new Dictionary<string, string> { [CompressionDictionaryExtension.Option.Name] = _directory }))!;

    private async Task StoreDictionaryAsync(byte[] dictionary, string field = UseAsDictionaryField) =>
        await Hook().ExecuteAsync(Context(DictionaryUrl), Origin(200, dictionary, field).Invoke);

    /// <summary>Stands in for curl's handler: writes one recorded response body to the output it is given.</summary>
    private sealed class OriginServer(int code, byte[] body, string[] fields)
    {
        public ITransferContext? Sent { get; private set; }

        public async ValueTask<TransferResult> Invoke(ITransferContext context) =>
            await RespondAsync(context, TransferResult.Success(body.Length));

        public async ValueTask<TransferResult> Failing(ITransferContext context) =>
            await RespondAsync(context, TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed", body.Length));

        public async ValueTask<TransferResult> WithoutReport(ITransferContext context)
        {
            Sent = context;
            await context.Output.WriteAsync(body);
            return TransferResult.Success(body.Length);
        }

        private async ValueTask<TransferResult> RespondAsync(ITransferContext context, TransferResult result)
        {
            Sent = context;
            await context.Output.WriteAsync(body);
            return result with
            {
                Report = new TransferReport
                {
                    ResponseCode = code,
                    ResponseHeaders = [.. fields.Select(field => field.Split(": ", 2)).Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]))],
                },
            };
        }
    }
}
