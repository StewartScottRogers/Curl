using System.IO.Compression;
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
    public async Task ExecuteAsync_NoDictionaryOffered_WritesTheBodyToTheOutputBeforeTheResponseEnds()
    {
        var context = Context(ResourceUrl);
        string? atFirstChunk = null;

        var result = await Hook().ExecuteAsync(context, async sent =>
        {
            await sent.Output.WriteAsync("first "u8.ToArray());
            atFirstChunk = Text(context.Output);
            await sent.Output.WriteAsync("second"u8.ToArray());
            return TransferResult.Success(12);
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("first ", atFirstChunk);
        Assert.AreEqual("first second", Text(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamedDictionaryAfterARedirect_IsStoredUnderTheEffectiveUrl_Rfc9842Section2_1()
    {
        var report = new TransferReport
        {
            ResponseCode = 200,
            EffectiveUrl = "https://other.example/app/dictionary.txt",
            ResponseHeaders = [new("Use-As-Dictionary", "match=\"/app/*\", id=\"v1\"")],
        };

        await Hook().ExecuteAsync(Context(ResourceUrl), async sent =>
        {
            await sent.Output.WriteAsync("dictionary"u8.ToArray());
            return TransferResult.Success(10) with { Report = report };
        });
        var elsewhere = Origin(200, "x");
        await Hook().ExecuteAsync(Context("https://other.example/app/main.js"), elsewhere.Invoke);
        var requested = Origin(200, "x");
        await Hook().ExecuteAsync(Context(ResourceUrl), requested.Invoke);

        Assert.IsTrue(elsewhere.Sent!.Http!.Headers.Any(line => line.StartsWith("Available-Dictionary:", StringComparison.Ordinal)));
        Assert.IsEmpty(requested.Sent!.Http!.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_StreamedBodyLongerThanTheStoredLimit_ReachesTheOutputAndIsNotStored()
    {
        var context = Context(DictionaryUrl);
        var body = new byte[CompressionDictionaryTransferHook.StoredBodyLimit + 1];

        await Hook().ExecuteAsync(context, async sent =>
        {
            await sent.Output.WriteAsync(body);
            return TransferResult.Success(body.Length) with
            {
                Report = new TransferReport { ResponseCode = 200, ResponseHeaders = [new("Use-As-Dictionary", "match=\"/app/*\"")] },
            };
        });

        Assert.AreEqual(body.Length, ((MemoryStream)context.Output).Length);
        Assert.IsFalse(Directory.Exists(_directory));
    }

    [TestMethod]
    public async Task CopyingStream_WritesThroughKeepsACopyUpToTheLimitAndRefusesReadingAndSeeking()
    {
        var destination = new MemoryStream();
        var copying = new CompressionDictionaryTransferHook.CopyingStream(destination, 4);

        copying.Write([1, 2], 0, 2);
        copying.Write([3]);
        await copying.WriteAsync(new byte[] { 4 });
        await copying.FlushAsync();
        copying.Flush();

        Assert.IsFalse(copying.Overflowed);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, copying.Copied);
        copying.Write([5]);
        Assert.IsTrue(copying.Overflowed);
        copying.Write([6]);
        Assert.AreEqual(6, destination.Length);
        Assert.IsFalse(copying.CanRead);
        Assert.IsFalse(copying.CanSeek);
        Assert.IsTrue(copying.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => copying.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => copying.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => copying.Position = 1);
        Assert.ThrowsExactly<NotSupportedException>(() => copying.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => copying.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => copying.SetLength(0));
    }
    [TestMethod]
    public async Task ExecuteAsync_StreamedDictionaryWithAnEffectiveUrlThatIsNotAbsolute_IsStoredUnderTheRequestedUrl()
    {
        var report = new TransferReport
        {
            ResponseCode = 200,
            EffectiveUrl = "not a url",
            ResponseHeaders = [new("Use-As-Dictionary", "match=\"/app/*\"")],
        };

        await Hook().ExecuteAsync(Context(ResourceUrl), _ => ValueTask.FromResult(TransferResult.Success(0) with { Report = report }));
        var requested = Origin(200, "x");
        await Hook().ExecuteAsync(Context(ResourceUrl), requested.Invoke);

        Assert.IsTrue(requested.Sent!.Http!.Headers.Any(line => line.StartsWith("Available-Dictionary:", StringComparison.Ordinal)));
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
    public async Task ExecuteAsync_RawOrAnAcceptEncodingHeaderGiven_AdvertisesNothing()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        foreach (var http in new[] { new HttpRequestOptions { Compressed = true, PassesContentCoding = true }, new HttpRequestOptions { Headers = ["accept-encoding: gzip"] } })
        {
            var origin = Origin(200, "next");

            await Hook().ExecuteAsync(Context(ResourceUrl, http), origin.Invoke);

            Assert.AreEqual(http.Headers.Count, origin.Sent!.Http!.Headers.Count);
            Assert.AreEqual(http.Compressed, origin.Sent.Http.Compressed);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedWithAMatchingDictionary_SendsCurlsCodingsThenTheDictionaryCodingsAndDecodesItself_Rfc9842Section6()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        var origin = Origin(200, "next");

        await Hook().ExecuteAsync(Context(ResourceUrl, new HttpRequestOptions { Compressed = true }), origin.Invoke);

        Assert.AreEqual("Accept-Encoding: deflate, gzip, br, zstd, dcb, dcz", origin.Sent!.Http!.Headers[^1]);
        Assert.IsFalse(origin.Sent.Http.Compressed);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedWithoutAMatchingDictionary_LeavesDecodingToCurl()
    {
        var context = Context(ResourceUrl, new HttpRequestOptions { Compressed = true });
        var origin = Origin(200, Gzip("next"), "Content-Encoding: gzip");

        await Hook().ExecuteAsync(context, origin.Invoke);

        Assert.IsTrue(origin.Sent!.Http!.Compressed);
        Assert.IsEmpty(origin.Sent.Http.Headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedAndADcbResponse_IsDecodedWithTheDictionary_Rfc9842Section4()
    {
        await StoreDictionaryAsync(BrotliRecordedStreams.Dictionary);
        var context = Context(ResourceUrl, new HttpRequestOptions { Compressed = true });
        var body = Framed(BrotliMagic, BrotliRecordedStreams.Dictionary, BrotliRecordedStreams.WithDictionaryQuality5Window10);

        var result = await Hook().ExecuteAsync(context, Origin(200, body, "Content-Encoding: dcb").Invoke);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(BrotliRecordedStreams.WithDictionarySha256, Convert.ToHexStringLower(SHA256.HashData(((MemoryStream)context.Output).ToArray())));
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedAndADczResponse_IsDecodedWithTheDictionary_Rfc9842Section5()
    {
        await StoreDictionaryAsync(DictionaryZstandardDecompressorTests.Dictionary);
        var context = Context(ResourceUrl, new HttpRequestOptions { Compressed = true });
        var body = Framed(ZstandardMagic, DictionaryZstandardDecompressorTests.Dictionary, Convert.FromHexString(DictionaryZstandardDecompressorTests.UsingDictionaryFrame));

        var result = await Hook().ExecuteAsync(context, Origin(200, body, "Content-Encoding: dcz").Invoke);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(DictionaryZstandardDecompressorTests.Content, ((MemoryStream)context.Output).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedAndCurlsOwnCodings_AreDecodedAsWithCompressedAlone()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        var cases = new (byte[] Body, string[] Fields)[]
        {
            (Gzip("plain"), ["Content-Encoding: gzip"]),
            (Gzip("plain"), ["Content-Encoding: X-GZIP"]),
            (Compress("plain", stream => new ZLibStream(stream, CompressionLevel.Optimal)), ["Content-Encoding: deflate"]),
            (Compress("plain", stream => new DeflateStream(stream, CompressionLevel.Optimal)), ["Content-Encoding: deflate"]),
            (Compress("plain", stream => new BrotliStream(stream, CompressionLevel.Optimal)), ["Content-Encoding: br"]),
            (Convert.FromHexString("28B52FFD2005290000706C61696E"), ["Content-Encoding: zstd"]),
            (Compress(Encoding.Latin1.GetString(Gzip("plain")), stream => new BrotliStream(stream, CompressionLevel.Optimal)), ["Content-Encoding: identity, gzip", "Content-Encoding: br"]),
            (Encoding.Latin1.GetBytes("plain"), ["Content-Encoding: none"]),
            (Encoding.Latin1.GetBytes("plain"), []),
        };
        foreach (var (body, fields) in cases)
        {
            var context = Context(ResourceUrl, new HttpRequestOptions { Compressed = true });

            var result = await Hook().ExecuteAsync(context, Origin(200, body, fields).Invoke);

            Assert.IsTrue(result.IsSuccess, string.Join(" ", fields));
            Assert.AreEqual("plain", Text(context.Output), string.Join(" ", fields));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_AnAdvertisedTransferWithNoResponseReport_IsWrittenAsReceived()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        foreach (var http in new[] { new HttpRequestOptions(), new HttpRequestOptions { Compressed = true } })
        {
            var context = Context(ResourceUrl, http);

            var result = await Hook().ExecuteAsync(context, Origin(200, "raw", "Content-Encoding: dcb").WithoutReport);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("raw", Text(context.Output));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CompressedAndACodingThatDoesNotDecode_FailsWithBadContentEncoding()
    {
        await StoreDictionaryAsync([1, 2, 3]);
        foreach (var (body, field) in new[] { ("plain", "Content-Encoding: compress"), ("not gzip", "Content-Encoding: gzip"), ("not zstd", "Content-Encoding: zstd") })
        {
            var context = Context(ResourceUrl, new HttpRequestOptions { Compressed = true });

            var result = await Hook().ExecuteAsync(context, Origin(200, body, field).Invoke);

            Assert.AreEqual(CurlExitCode.BadContentEncoding, result.ExitCode, field);
            Assert.AreEqual(0, ((MemoryStream)context.Output).Length);
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

    private static byte[] Gzip(string text) => Compress(text, stream => new GZipStream(stream, CompressionLevel.Optimal));

    private static byte[] Compress(string text, Func<Stream, Stream> compressor)
    {
        using MemoryStream compressed = new();
        using (var stream = compressor(compressed))
        {
            stream.Write(Encoding.Latin1.GetBytes(text));
        }

        return compressed.ToArray();
    }

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
