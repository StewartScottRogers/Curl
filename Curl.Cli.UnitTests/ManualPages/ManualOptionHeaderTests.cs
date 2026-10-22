using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualOptionHeaderTests
{
    private const string Required =
        "Long: verbose\nCategory: important\nExample: -v $URL\nAdded: 8.0\nSee-also: silent\nC: Copyright\nSPDX-License-Identifier: curl\n";

    [TestMethod]
    public void Parse_EveryField_ReadsEachIntoItsProperty()
    {
        string text = "intro\n---\n<!-- comment -->\nC: Copyright\nSPDX-License-Identifier: curl\nLong: verbose\nShort: v\nArg: <x>\n"
            + "Added: 4.0\nCategory: important\nHelp: Talk\nTags: x\nMagic: m\nMutexed: silent\nProtocols: HTTP\nRequires: TLS\n"
            + "Multi: boolean\nScope: global\nExperimental: yes\nSee-also:\n  - silent\n  - trace\nExample:\n  - -v $URL\n  - -s $URL\n---\nbody\n";

        (ManualOptionHeader header, int? failure, string error) = Parse(text);

        Assert.IsNull(failure);
        Assert.AreEqual("", error);
        Assert.AreEqual("v", header.Short);
        Assert.AreEqual("verbose", header.Long);
        Assert.AreEqual("<x>", header.Arg);
        Assert.AreEqual("4.0", header.Added);
        Assert.AreEqual("important", header.Category);
        Assert.AreEqual("silent", header.Mutexed);
        Assert.AreEqual("HTTP", header.Protocols);
        Assert.AreEqual("TLS", header.Requires);
        Assert.AreEqual("boolean", header.Multi);
        Assert.AreEqual("global", header.Scope);
        Assert.IsTrue(header.Experimental);
        Assert.AreEqual("Copyright", header.Copyright);
        Assert.AreEqual("curl", header.Spdx);
        CollectionAssert.AreEqual(new[] { "silent", "trace" }, header.SeeAlso);
        CollectionAssert.AreEqual(new[] { "-v $URL", "-s $URL" }, header.Examples);
        Assert.AreEqual(26, header.LineNumber);
        Assert.AreEqual(26, header.BodyStart);
    }

    [TestMethod]
    public void Parse_RequiredFieldsOnly_LeavesOptionalFieldsUnset()
    {
        (ManualOptionHeader header, int? failure, _) = Parse($"---\n{Required}---\n");

        Assert.IsNull(failure);
        Assert.IsNull(header.Short);
        Assert.IsNull(header.Arg);
        Assert.IsNull(header.Scope);
        Assert.IsFalse(header.Experimental);
        Assert.AreEqual("", header.Multi);
        CollectionAssert.AreEqual(new[] { "silent" }, header.SeeAlso);
        CollectionAssert.AreEqual(new[] { "-v $URL" }, header.Examples);
    }

    [TestMethod]
    public void Parse_CommentInsideHeader_IsSkipped()
    {
        (_, int? failure, string error) = Parse($"---\n  <!-- Long: other -->\n{Required}---\n");

        Assert.IsNull(failure);
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public void Parse_NoClosingLine_RefusesWithNoProperHeader()
    {
        (_, int? failure, string error) = Parse($"---\n{Required}");

        Assert.AreEqual(2, failure);
        Assert.AreEqual("opt.md:1:1:ERROR: no proper meta-data header\n", error);
    }

    [TestMethod]
    public void Parse_NoOpeningLine_RefusesWithNoProperHeader()
    {
        (_, int? failure, string error) = Parse(Required);

        Assert.AreEqual(2, failure);
        Assert.AreEqual("opt.md:1:1:ERROR: no proper meta-data header\n", error);
    }

    [TestMethod]
    public void Parse_UnknownField_WarnsAndCarriesOn()
    {
        (_, int? failure, string error) = Parse($"---\nColour: blue\n{Required}---\n");

        Assert.IsNull(failure);
        Assert.AreEqual("opt.md:2:1:WARN: unrecognized line in opt.md, ignoring: 'Colour: blue';\n", error);
    }

    [TestMethod]
    public void Parse_ListItemBeforeAnyList_WarnsAndIgnoresIt()
    {
        (ManualOptionHeader header, int? failure, string error) = Parse($"---\n  - stray\n{Required}---\n");

        Assert.IsNull(failure);
        Assert.AreEqual("opt.md:2:1:WARN: unrecognized line in opt.md, ignoring: '  - stray';\n", error);
        CollectionAssert.AreEqual(new[] { "silent" }, header.SeeAlso);
    }

    [TestMethod]
    public void Parse_SecondInlineSeeAlso_RefusesAsDuplicated()
    {
        (_, int? failure, string error) = Parse($"---\n{Required}See-also: trace\n---\n");

        Assert.AreEqual(1, failure);
        Assert.AreEqual("ERROR: duplicated See-also in opt.md\n", error);
    }

    [TestMethod]
    public void Parse_NoLong_RefusesWithOne()
    {
        (_, int? failure, string error) = Parse(Without("Long: verbose\n"));

        Assert.AreEqual(1, failure);
        Assert.AreEqual("ERROR: no 'Long:' in opt.md\n", error);
    }

    [TestMethod]
    public void Parse_NoCategory_RefusesWithTwo()
    {
        (_, int? failure, string error) = Parse(Without("Category: important\n"));

        Assert.AreEqual(2, failure);
        Assert.AreEqual("ERROR: no 'Category:' in opt.md\n", error);
    }

    [TestMethod]
    [DataRow("Example: -v $URL\n", "", "no 'Example:' present")]
    [DataRow("Example: -v $URL\n", "Example: 0\n", "no 'Example:' present")]
    [DataRow("Added: 8.0\n", "", "no 'Added:' version present")]
    [DataRow("See-also: silent\n", "", "no 'See-also:' field present")]
    [DataRow("See-also: silent\n", "See-also: 0\n", "no 'See-also:' field present")]
    [DataRow("C: Copyright\n", "", "no 'C:' field present")]
    [DataRow("SPDX-License-Identifier: curl\n", "", "no 'SPDX-License-Identifier:' field present")]
    public void Parse_MissingOrFalseRequiredField_RefusesNamingTheField(string field, string replacement, string message)
    {
        string text = Without(field, replacement);
        int closing = text.Split('\n').Length - 1;

        (_, int? failure, string error) = Parse(text);

        Assert.AreEqual(2, failure);
        Assert.AreEqual($"opt.md:{closing}:1:ERROR: {message}\n", error);
    }

    private static string Without(string field, string replacement = "") => $"---\n{Required.Replace(field, replacement, StringComparison.Ordinal)}---\n";

    private static (ManualOptionHeader Header, int? Failure, string Error) Parse(string text)
    {
        ManualPageContext context = new(_ => null, "2026-10-21", new StringBuilder(), new StringBuilder());
        (ManualOptionHeader header, int? failure) = ManualOptionHeader.Parse(context, "opt.md", ManualPerlValues.Lines(text));
        return (header, failure, context.Error.ToString());
    }
}
