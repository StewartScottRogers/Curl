using Curl.Cli;
using Curl.Core.Globbing;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins what one transfer takes from its command-line URL's glob match and output entry: the
/// URL, the positions, and the <c>-o</c> name with each <c>#N</c> substituted, as curl 8.21.0
/// names it (BL-240 Notes).
/// </summary>
[TestClass]
public sealed class UrlTransferTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_OutputNameWithHash_SubstitutesTheGlobValue()
    {
        UrlTransfer transfer = Create(Parse("-o", "o#1", "http://h/{a,b}"), 0, 0, 1, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Diagnostics.Assert(
            "URL / URL index / transfer id / output name / remote name / to file",
            "http://h/b / 0 / 1 / ob / False / True",
            $"{transfer.Url} / {transfer.UrlIndex} / {transfer.TransferId} / {transfer.OutputFileName} / {transfer.UsesRemoteName} / {transfer.WritesToFile}");
        Assert.AreEqual("http://h/b", transfer.Url);
        Assert.AreEqual(0, transfer.UrlIndex);
        Assert.AreEqual(1L, transfer.TransferId);
        Assert.AreEqual("ob", transfer.OutputFileName);
        Assert.IsFalse(transfer.UsesRemoteName);
        Assert.IsTrue(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_OutputNameOnWindows_IsSanitizedAfterSubstitution()
    {
        UrlTransfer transfer = Create(Parse("-o", "#1?", "http://h/{a,b}"), 0, 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: true);

        Diagnostics.Assert("output name", "b_", transfer.OutputFileName);
        Assert.AreEqual("b_", transfer.OutputFileName);
    }

    [TestMethod]
    public void Constructor_RemoteName_WritesToFileWithoutAnOutputName()
    {
        UrlTransfer transfer = Create(Parse("-O", "http://h/{a,b}"), 0, 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Diagnostics.Assert("output name / remote name / to file", "(none) / True / True", $"{transfer.OutputFileName ?? "(none)"} / {transfer.UsesRemoteName} / {transfer.WritesToFile}");
        Assert.IsNull(transfer.OutputFileName);
        Assert.IsTrue(transfer.UsesRemoteName);
        Assert.IsTrue(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_UrlWithoutOutputEntry_WritesToStandardOutput()
    {
        UrlTransfer transfer = Create(Parse("-o", "first", "http://h/x", "http://h/{a,b}"), 1, 5, 2, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Diagnostics.Assert(
            "URL index / URL number / output name / remote name / to file",
            "1 / 5 / (none) / False / False",
            $"{transfer.UrlIndex} / {transfer.UrlNumber} / {transfer.OutputFileName ?? "(none)"} / {transfer.UsesRemoteName} / {transfer.WritesToFile}");
        Assert.AreEqual(1, transfer.UrlIndex);
        Assert.AreEqual(5, transfer.UrlNumber);
        Assert.IsNull(transfer.OutputFileName);
        Assert.IsFalse(transfer.UsesRemoteName);
        Assert.IsFalse(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_OutputNameDash_WritesToStandardOutput()
    {
        UrlTransfer transfer = Create(Parse("--output-dir", "d", "-o", "-", "http://h/{a,b}"), 0, 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: true);

        Diagnostics.Assert("output name / to file", "(none) / False", $"{transfer.OutputFileName ?? "(none)"} / {transfer.WritesToFile}");
        Assert.IsNull(transfer.OutputFileName);
        Assert.IsFalse(transfer.WritesToFile);
    }

    [TestMethod]
    public void TryCreate_NamedReferenceToNoGlob_FailsWithExit43AndNoTransfer()
    {
        bool created = UrlTransfer.TryCreate(Parse("-o", "somewhere/#<foo>", "http://h/{<test>A,B}{<moo>C,D}"), 0, 0, 0, SecondMatch("http://h/{<test>A,B}{<moo>C,D}"), uploadMatch: null, sanitizesForWindows: false, out UrlTransfer? transfer, out TransferResult? failure);
        Diagnostics.Act("created / transfer / exit code / error", $"{created} / {(transfer is null ? "(none)" : "made")} / {failure?.ExitCode} / {failure?.ErrorMessage}");

        Diagnostics.Assert(
            "created / transfer / exit code / error",
            "False / (none) / BadFunctionArgument / no glob exists with this name in position 16:\nsomewhere/#<foo>\n               ^",
            $"{created} / {(transfer is null ? "(none)" : "made")} / {failure?.ExitCode} / {failure?.ErrorMessage}");
        Assert.IsFalse(created);
        Assert.IsNull(transfer);
        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure.ExitCode);
        Assert.AreEqual("no glob exists with this name in position 16:\nsomewhere/#<foo>\n               ^", failure.ErrorMessage);
    }

    [TestMethod]
    public void TryCreate_NamedReferenceToUploadGlob_TakesTheUploadGlobValueAndFile()
    {
        Assert.IsTrue(UploadFileGlob.TryParse("{<f>a,b}", globOff: false, out UploadFileGlob? upload, out TransferResult? _));
        Diagnostics.Arrange("upload file glob / upload match", "{<f>a,b} / second");

        UrlTransfer transfer = Create(Parse("-o", "#<f>-#1", "http://h/{x,y}"), 0, 0, 0, SecondMatch("http://h/{x,y}"), upload.ExpandUploadMatches().ElementAt(1), sanitizesForWindows: false);

        Diagnostics.Assert("output name / upload file", "b-y / b", $"{transfer.OutputFileName} / {transfer.UploadFile}");
        Assert.AreEqual("b-y", transfer.OutputFileName);
        Assert.AreEqual("b", transfer.UploadFile);
    }

    private UrlTransfer Create(CommandLineOptions options, int urlIndex, int urlNumber, long transferId, UrlGlobMatch match, bool sanitizesForWindows) =>
        Create(options, urlIndex, urlNumber, transferId, match, uploadMatch: null, sanitizesForWindows);

    private UrlTransfer Create(CommandLineOptions options, int urlIndex, int urlNumber, long transferId, UrlGlobMatch match, UrlGlobMatch? uploadMatch, bool sanitizesForWindows)
    {
        Diagnostics.Arrange("URL index / URL number / transfer id / sanitizes for Windows", $"{urlIndex} / {urlNumber} / {transferId} / {sanitizesForWindows}");
        Assert.IsTrue(UrlTransfer.TryCreate(options, urlIndex, urlNumber, transferId, match, uploadMatch, sanitizesForWindows, out UrlTransfer? transfer, out TransferResult? _));
        Diagnostics.Act(
            "URL / output name / remote name / to file / upload file",
            $"{transfer.Url} / {transfer.OutputFileName ?? "(none)"} / {transfer.UsesRemoteName} / {transfer.WritesToFile} / {transfer.UploadFile ?? "(none)"}");
        return transfer;
    }

    private static UrlGlobMatch SecondMatch(string url)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? _));
        return glob.Expand().ElementAt(1);
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("command line (second glob match)", string.Join(' ', arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
