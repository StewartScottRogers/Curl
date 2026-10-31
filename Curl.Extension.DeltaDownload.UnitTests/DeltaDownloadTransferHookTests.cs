using System.Text;

namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class DeltaDownloadTransferHookTests
{
    private static readonly byte[] Target = Encoding.ASCII.GetBytes("AAAABBBBCCCCDDDDEEEEFF");

    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/file.bin");

    private readonly string directory = Path.Combine(Path.GetTempPath(), "curl-zsync-" + Guid.NewGuid().ToString("N"));

    [TestInitialize]
    public void CreateDirectory() => Directory.CreateDirectory(directory);

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task ExecuteAsync_ASeedLackingTwoRuns_AsksOnlyForTheirRangesAndWritesTheWholeFile_Zsync06BlockChecksums()
    {
        var origin = new ByteRangeOrigin(Target);
        var context = Context();

        var result = await Hook("AAAAxxxxCCCCDDDDyyyy").ExecuteAsync(context, origin.Invoke);

        CollectionAssert.AreEqual(new[] { "bytes=4-7", "bytes=16-21" }, origin.Ranges);
        CollectionAssert.AreEqual(Target, Bytes(context.Output));
        Assert.AreEqual("HTTP/1.1 206 X\r\nContent-Range: bytes 4-7/22\r\n\r\n", Encoding.Latin1.GetString(Bytes(context.HeaderOutput!)));
        Assert.AreEqual(22, result.BytesTransferred);
        Assert.AreEqual(206, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ASeedHoldingEveryBlock_ContactsNoServerAndWritesTheFile()
    {
        var context = Context();

        var result = await Hook(Encoding.ASCII.GetString(Target)).ExecuteAsync(context, NoServer);

        CollectionAssert.AreEqual(Target, Bytes(context.Output));
        Assert.AreEqual(0, context.HeaderOutput!.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(22, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_AServerThatIgnoresRange_IsWrittenAsReceived_Rfc9110Section14_2()
    {
        var origin = new ByteRangeOrigin(Target) { Rewrite = _ => new(200, [], Target) };
        var context = Context();

        var result = await Hook("AAAA").ExecuteAsync(context, origin.Invoke);

        Assert.HasCount(1, origin.Ranges);
        CollectionAssert.AreEqual(Target, Bytes(context.Output));
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedRange_FailsTheTransferWithNothingWritten()
    {
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");
        var origin = new ByteRangeOrigin(Target) { Rewrite = response => response with { Result = failure } };
        var context = Context();

        var result = await Hook("AAAA").ExecuteAsync(context, origin.Invoke);

        Assert.AreSame(failure, result);
        Assert.AreEqual(0, context.Output.Length);
    }

    [TestMethod]
    [DataRow("code")]
    [DataRow("content-range")]
    [DataRow("length")]
    public async Task ExecuteAsync_ARangeAnsweredWithAnythingElse_RunsTheTransferAgainAsCurlDoes(string defect)
    {
        var origin = new ByteRangeOrigin(Target) { Rewrite = response => response.Code == 206 ? Spoil(response, defect) : response };
        var context = Context();

        var result = await Hook("AAAA").ExecuteAsync(context, origin.Invoke);

        CollectionAssert.AreEqual(new[] { "bytes=4-21", null }, origin.Ranges);
        CollectionAssert.AreEqual(Target, Bytes(context.Output));
        Assert.AreEqual(200, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AJoinedFileWhoseSha1Differs_RunsTheTransferAgainAsCurlDoes_Zsync06Sha1()
    {
        var changed = Encoding.ASCII.GetBytes("AAAABBBBCCCCDDDDEEEEFG");
        var origin = new ByteRangeOrigin(changed);
        var context = Context();

        await Hook("AAAA").ExecuteAsync(context, origin.Invoke);

        CollectionAssert.AreEqual(new[] { "bytes=4-21", null }, origin.Ranges);
        CollectionAssert.AreEqual(changed, Bytes(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnythingButAPlainGet_RunsAsCurlDoes()
    {
        var origin = new ByteRangeOrigin(Target);

        await new DeltaDownloadTransferHook(Path.Combine(directory, "absent.zsync"), null).ExecuteAsync(new TransferContext { Url = Url, Output = new MemoryStream(), HeaderOutput = new MemoryStream(), Http = new HttpRequestOptions(), NoBody = true }, origin.Invoke);

        CollectionAssert.AreEqual(new string?[] { null }, origin.Ranges);
    }

    [TestMethod]
    public async Task ExecuteAsync_AControlFileItCannotRead_RefusesTheTransferWithExit26()
    {
        var path = Path.Combine(directory, "absent.zsync");

        var result = await new DeltaDownloadTransferHook(path, null).ExecuteAsync(Context(), NoServer);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual($"--zsync could not read the control file {path}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFileThatIsNotAControlFile_RefusesTheTransferWithExit26()
    {
        var path = Write("broken.zsync", "zsync: 0.6.2\n"u8.ToArray());

        var result = await new DeltaDownloadTransferHook(path, null).ExecuteAsync(Context(), NoServer);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual($"--zsync: {path} is not a zsync control file", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("curl-zsync-no-such-seed.bin")]
    [DataRow(null)]
    public async Task ExecuteAsync_NoSeedGiven_TakesTheControlFilesFilenameInTheCurrentDirectoryOrFetchesEverything(string? fileName)
    {
        var origin = new ByteRangeOrigin(Target);
        var context = Context();

        await new DeltaDownloadTransferHook(Write("named.zsync", ZsyncFile.For(Target, 4, fileName: fileName)), null).ExecuteAsync(context, origin.Invoke);

        CollectionAssert.AreEqual(new[] { "bytes=0-21" }, origin.Ranges);
        CollectionAssert.AreEqual(Target, Bytes(context.Output));
    }

    [TestMethod]
    public async Task ExecuteAsync_ASeedThatIsADirectory_FetchesEverything()
    {
        var origin = new ByteRangeOrigin(Target);

        await new DeltaDownloadTransferHook(Write("dir.zsync", ZsyncFile.For(Target, 4)), directory).ExecuteAsync(Context(), origin.Invoke);

        CollectionAssert.AreEqual(new[] { "bytes=0-21" }, origin.Ranges);
    }

    private static ValueTask<TransferResult> NoServer(ITransferContext context) => throw new AssertFailedException("The server was contacted.");

    private static byte[] Bytes(Stream stream) => ((MemoryStream)stream).ToArray();

    private static ByteRangeOrigin.Response Spoil(ByteRangeOrigin.Response response, string defect) => defect switch
    {
        "code" => response with { Code = 299 },
        "content-range" => response with { Fields = [new("Content-Range", "bytes 4-21/23")] },
        _ => response with { Body = response.Body[1..] },
    };

    private DeltaDownloadTransferHook Hook(string seed) =>
        new(Write("file.zsync", ZsyncFile.For(Target, 4)), Write("seed.bin", Encoding.ASCII.GetBytes(seed)));

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static TransferContext Context() => new()
    {
        Url = Url,
        Output = new MemoryStream(),
        HeaderOutput = new MemoryStream(),
        Http = new HttpRequestOptions(),
    };
}
