using System.Text;
using Curl.Authentication;

namespace Curl.Console;

/// <summary>
/// CGR-015 part 1a (BL-3892, ADR-0838): on Windows the reference curl reads its <c>argv</c> through the
/// system ANSI code page with best fit. The code page is passed in (Windows-1252, as on the measuring
/// machine) so these pass on every host.
/// </summary>
[TestClass]
public sealed class Cgr015ArgumentTests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    public void ThroughCodePage_CharactersTheCodePageLacks_BecomeTheirBestFitOrQuestionMark()
    {
        string[] arguments = ["中.txt", "http://中文.localhost:20000/", "http://127.0.0.1:2020０/a"];

        IReadOnlyList<string> read = AnsiCodePageArguments.ThroughCodePage(arguments, Windows1252);

        CollectionAssert.AreEqual(
            new[] { "?.txt", "http://??.localhost:20000/", "http://127.0.0.1:20200/a" },
            read.ToArray());
    }

    [TestMethod]
    public void ThroughCodePage_CharactersTheCodePageHas_AreKept()
    {
        string[] arguments = ["--sílent", "-d", "“é”"];

        IReadOnlyList<string> read = AnsiCodePageArguments.ThroughCodePage(arguments, Windows1252);

        CollectionAssert.AreEqual(arguments, read.ToArray());
    }

    [TestMethod]
    public void ForPlatform_OffWindows_ReturnsTheArgumentsUnchanged()
    {
        string[] arguments = ["中.txt", "http://127.0.0.1:2020０/a"];

        IReadOnlyList<string> read = AnsiCodePageArguments.ForPlatform(arguments, isWindows: false);

        Assert.AreSame(arguments, read);
    }

    [TestMethod]
    public void ForPlatform_OnWindows_ReadsThroughTheSystemAnsiCodePage()
    {
        string[] arguments = ["http://127.0.0.1:2020０/a"];

        IReadOnlyList<string> read = AnsiCodePageArguments.ForPlatform(arguments, isWindows: true);

        CollectionAssert.AreEqual(
            AnsiCodePageArguments.ThroughCodePage(arguments, CredentialEncoding.ForPlatform(isWindows: true)).ToArray(),
            read.ToArray());
    }
}
