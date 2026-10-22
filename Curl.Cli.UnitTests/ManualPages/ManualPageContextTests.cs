using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualPageContextTests
{
    [TestMethod]
    public void New_NoArgumentsGiven_StartsWithTheScriptsDefaults()
    {
        ManualPageContext context = NewContext(_ => null);

        Assert.AreEqual(".", context.Directory);
        Assert.AreEqual(79, context.ColumnWidth);
        Assert.AreEqual("unknown", context.Version);
        Assert.AreEqual("", context.Globals);
        Assert.AreEqual(0, context.Errors);
        Assert.AreEqual("2026-10-21", context.Date);
    }

    [TestMethod]
    public void ReadLines_FileExists_ReturnsItsLinesWithTheirNewlines()
    {
        ManualPageContext context = NewContext(path => path == "/d/a.md" ? "one\ntwo\n" : null);

        List<string> lines = context.ReadLines("/d/a.md", "no /d/a.md");

        CollectionAssert.AreEqual(new[] { "one\n", "two\n" }, lines);
        Assert.AreEqual("", context.Error.ToString());
    }

    [TestMethod]
    public void ReadLines_FileMissing_DiesWithItsMessageAndExitCode2()
    {
        ManualPageContext context = NewContext(_ => null);

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => context.ReadLines("/d/a.md", "no /d/a.md"));

        Assert.AreEqual(2, exit.ExitCode);
        Assert.AreEqual("no /d/a.md\n", context.Error.ToString());
    }

    [TestMethod]
    public void PathOf_DirectorySet_JoinsItAndTheFileNameWithASlash()
    {
        ManualPageContext context = NewContext(_ => null);
        context.Directory = "/docs";

        Assert.AreEqual("/docs/mainpage.idx", context.PathOf("mainpage.idx"));
    }

    private static ManualPageContext NewContext(Func<string, string?> readFile) => new(readFile, "2026-10-21", new StringBuilder(), new StringBuilder());
}
