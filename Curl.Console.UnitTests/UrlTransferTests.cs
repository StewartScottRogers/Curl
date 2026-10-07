using Curl.Cli;
using Curl.Core.Globbing;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins what one transfer takes from its command-line URL's glob match and output entry: the
/// URL, the positions, and the <c>-o</c> name with each <c>#N</c> substituted, as curl 8.21.0
/// names it (BL-240 Notes).
/// </summary>
[TestClass]
public sealed class UrlTransferTests
{
    [TestMethod]
    public void Constructor_OutputNameWithHash_SubstitutesTheGlobValue()
    {
        UrlTransfer transfer = Create(Parse("-o", "o#1", "http://h/{a,b}"), 0, 0, 1, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

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

        Assert.AreEqual("b_", transfer.OutputFileName);
    }

    [TestMethod]
    public void Constructor_RemoteName_WritesToFileWithoutAnOutputName()
    {
        UrlTransfer transfer = Create(Parse("-O", "http://h/{a,b}"), 0, 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Assert.IsNull(transfer.OutputFileName);
        Assert.IsTrue(transfer.UsesRemoteName);
        Assert.IsTrue(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_UrlWithoutOutputEntry_WritesToStandardOutput()
    {
        UrlTransfer transfer = Create(Parse("-o", "first", "http://h/x", "http://h/{a,b}"), 1, 5, 2, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

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

        Assert.IsNull(transfer.OutputFileName);
        Assert.IsFalse(transfer.WritesToFile);
    }

    [TestMethod]
    public void TryCreate_NamedReferenceToNoGlob_FailsWithExit43AndNoTransfer()
    {
        bool created = UrlTransfer.TryCreate(Parse("-o", "somewhere/#<foo>", "http://h/{<test>A,B}{<moo>C,D}"), 0, 0, 0, SecondMatch("http://h/{<test>A,B}{<moo>C,D}"), uploadMatch: null, sanitizesForWindows: false, out UrlTransfer? transfer, out TransferResult? failure);

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

        UrlTransfer transfer = Create(Parse("-o", "#<f>-#1", "http://h/{x,y}"), 0, 0, 0, SecondMatch("http://h/{x,y}"), upload.ExpandUploadMatches().ElementAt(1), sanitizesForWindows: false);

        Assert.AreEqual("b-y", transfer.OutputFileName);
        Assert.AreEqual("b", transfer.UploadFile);
    }

    private static UrlTransfer Create(CommandLineOptions options, int urlIndex, int urlNumber, long transferId, UrlGlobMatch match, bool sanitizesForWindows) =>
        Create(options, urlIndex, urlNumber, transferId, match, uploadMatch: null, sanitizesForWindows);

    private static UrlTransfer Create(CommandLineOptions options, int urlIndex, int urlNumber, long transferId, UrlGlobMatch match, UrlGlobMatch? uploadMatch, bool sanitizesForWindows)
    {
        Assert.IsTrue(UrlTransfer.TryCreate(options, urlIndex, urlNumber, transferId, match, uploadMatch, sanitizesForWindows, out UrlTransfer? transfer, out TransferResult? _));
        return transfer;
    }

    private static UrlGlobMatch SecondMatch(string url)
    {
        Assert.IsTrue(UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? _));
        return glob.Expand().ElementAt(1);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
