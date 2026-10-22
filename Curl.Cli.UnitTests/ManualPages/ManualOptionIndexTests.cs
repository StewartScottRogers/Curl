using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualOptionIndexTests
{
    [TestMethod]
    public void Add_RecordsShortLongAndHelpFromTheHeaderOnly()
    {
        ManualPageContext context = Context(new()
        {
            ["./opt.md"] = "intro\n---\nShort: v\nLong: verbose\nHelp: Make it talk\n---\nLong: ignored\n",
        });

        context.Index.Add(context, "docs/opt.md");

        Assert.IsTrue(context.Index.HasShort("v"));
        Assert.IsTrue(context.Index.HasHelp("verbose"));
        Assert.IsFalse(context.Index.HasHelp("ignored"));
        Assert.AreEqual("", context.Error.ToString());
    }

    [TestMethod]
    public void Add_WithoutShortOrHelp_RecordsNeither()
    {
        ManualPageContext context = Context(new() { ["./opt.md"] = "---\nLong: verbose\n" });

        context.Index.Add(context, "opt.md");

        Assert.IsFalse(context.Index.HasShort("v"));
        Assert.IsFalse(context.Index.HasHelp("verbose"));
    }

    [TestMethod]
    public void Add_WithNoHeader_RecordsNothing()
    {
        ManualPageContext context = Context(new() { ["./opt.md"] = "Short: v\n" });

        context.Index.Add(context, "opt.md");

        Assert.IsFalse(context.Index.HasShort("v"));
    }

    [TestMethod]
    public void Add_OversizedHelp_Warns()
    {
        string help = new('x', 49);
        ManualPageContext context = Context(new() { ["./opt.md"] = $"---\nLong: big\nHelp: {help}\n---\n" });

        context.Index.Add(context, "opt.md");

        Assert.AreEqual("opt.md:3:1:WARN: oversized help text: 49 characters\n", context.Error.ToString());
    }

    [TestMethod]
    public void Add_MissingFile_ExitsWithTwo()
    {
        ManualPageContext context = Context([]);

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => context.Index.Add(context, "opt.md"));

        Assert.AreEqual("could not find ./opt.md\n", context.Error.ToString());
        Assert.AreEqual(2, exit.ExitCode);
    }

    private static ManualPageContext Context(Dictionary<string, string> files) =>
        new(path => files.GetValueOrDefault(path), "2026-10-21", new StringBuilder(), new StringBuilder());
}
