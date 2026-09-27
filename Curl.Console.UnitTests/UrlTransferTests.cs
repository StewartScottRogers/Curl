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
        UrlTransfer transfer = new(Parse("-o", "o#1", "http://h/{a,b}"), 0, 1, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

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
        UrlTransfer transfer = new(Parse("-o", "#1?", "http://h/{a,b}"), 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: true);

        Assert.AreEqual("b_", transfer.OutputFileName);
    }

    [TestMethod]
    public void Constructor_RemoteName_WritesToFileWithoutAnOutputName()
    {
        UrlTransfer transfer = new(Parse("-O", "http://h/{a,b}"), 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Assert.IsNull(transfer.OutputFileName);
        Assert.IsTrue(transfer.UsesRemoteName);
        Assert.IsTrue(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_UrlWithoutOutputEntry_WritesToStandardOutput()
    {
        UrlTransfer transfer = new(Parse("-o", "first", "http://h/x", "http://h/{a,b}"), 1, 2, SecondMatch("http://h/{a,b}"), sanitizesForWindows: false);

        Assert.AreEqual(1, transfer.UrlIndex);
        Assert.IsNull(transfer.OutputFileName);
        Assert.IsFalse(transfer.UsesRemoteName);
        Assert.IsFalse(transfer.WritesToFile);
    }

    [TestMethod]
    public void Constructor_OutputNameDash_WritesToStandardOutput()
    {
        UrlTransfer transfer = new(Parse("--output-dir", "d", "-o", "-", "http://h/{a,b}"), 0, 0, SecondMatch("http://h/{a,b}"), sanitizesForWindows: true);

        Assert.IsNull(transfer.OutputFileName);
        Assert.IsFalse(transfer.WritesToFile);
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
