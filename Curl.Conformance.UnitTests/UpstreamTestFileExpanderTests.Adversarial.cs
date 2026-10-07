using System.Text;

namespace Curl.Conformance;

// Attacks the expander through its public surface with instructions cut short or out of range,
// replacements that rebuild an instruction, self-including files, stray and deeply nested %if
// blocks, and concurrent calls, by Documentation/Wiki/Adversarial-Testing.md (BL-1494).
public sealed partial class UpstreamTestFileExpanderTests
{
    [TestMethod]
    [DataRow("%repeat[99999999999 x a]%\n", DisplayName = "count past int.MaxValue")]
    [DataRow("%repeat[ x a]%\n", DisplayName = "no count")]
    [DataRow("%repeat[2xa]%\n", DisplayName = "no spaces around x")]
    [DataRow("%repeat[2 x a\n", DisplayName = "no closing")]
    [DataRow("%b64[abc\n", DisplayName = "b64 with no closing")]
    [DataRow("%hex[41\n", DisplayName = "hex with no closing")]
    public void Expand_InstructionThatDoesNotMatch_IsLeftAsWritten(string line)
    {
        UpstreamTestFileExpansion expansion = Expand(line, NoVariables);

        Diagnostics.Assert("expanded text", line, Text(expansion));
        Assert.AreEqual(line, Text(expansion));
    }

    [TestMethod]
    public void Expand_RepeatCountOfZero_LeavesNothing()
    {
        UpstreamTestFileExpansion expansion = Expand("<%repeat[0 x abc]%>\n", NoVariables);

        Diagnostics.Assert("expanded text", "<>\n", Text(expansion));
        Assert.AreEqual("<>\n", Text(expansion));
    }

    [TestMethod]
    [DataRow("%hex[%4]hex%\n", "%4\n", DisplayName = "pair cut short")]
    [DataRow("%hex[%zz41]hex%\n", "%zz41\n", DisplayName = "pair that is not hexadecimal")]
    [DataRow("%hex[%]hex%\n", "%\n", DisplayName = "lone percent")]
    public void Expand_HexWithAMalformedPair_KeepsItsCharacters(string line, string expected)
    {
        UpstreamTestFileExpansion expansion = Expand(line, NoVariables);

        Diagnostics.Assert("expanded text", expected, Text(expansion));
        Assert.AreEqual(expected, Text(expansion));
    }

    [TestMethod]
    public void Expand_HexThatDecodesToAnotherHexInstruction_ExpandsItUntilNoneIsLeft()
    {
        UpstreamTestFileExpansion expansion = Expand("%hex[%25hex[%5dhex%25]hex%\n", NoVariables);

        Diagnostics.Assert("expanded text", "\n", Text(expansion));
        Assert.AreEqual("\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_VariableWhoseValueNamesItself_IsSubstitutedOnce()
    {
        UpstreamTestFileExpansion expansion = Expand("%TESTNUMBER\n", new Dictionary<string, string> { ["TESTNUMBER"] = "%TESTNUMBER" });

        Diagnostics.Assert("expanded text", "%TESTNUMBER\n", Text(expansion));
        Assert.AreEqual("%TESTNUMBER\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_VariableWithAnEmptyName_IsNeverMatched()
    {
        UpstreamTestFileExpansion expansion = Expand("100%\n", new Dictionary<string, string> { [string.Empty] = "boom" });

        Diagnostics.Assert("expanded text", "100%\n", Text(expansion));
        Assert.AreEqual("100%\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_IncludedFileThatIncludesItself_IsReadOnceAndNotSearchedAgain()
    {
        int reads = 0;
        Diagnostics.Arrange("self", "%include self%\\n");

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(
            "%include self%\n"u8, NoVariables, NoFeatures, _ => { reads++; return "%include self%\n"u8.ToArray(); });

        Diagnostics.Assert("reads", 1, reads);
        Assert.AreEqual(1, reads);
        Assert.AreEqual("%include self%\n", Text(expansion));
    }

    [TestMethod]
    public void Expand_IncludedTextThatIncludesItselfRaw_IsReadOncePerPass()
    {
        int reads = 0;

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(
            "%includetext self%\n"u8, NoVariables, NoFeatures, _ => { reads++; return "%include self%\n"u8.ToArray(); });

        Diagnostics.Assert("reads", 2, reads);
        Assert.AreEqual(2, reads);
        Assert.AreEqual("%include self%\n", Text(expansion));
    }

    [TestMethod]
    [DataRow("a\n%endif\nb\n", "a\n", DisplayName = "stray endif")]
    [DataRow("%else\nb\n", "", DisplayName = "stray else on the first line")]
    public void Expand_StrayConditionDirective_StopsAndNamesIt(string file, string expected)
    {
        UpstreamTestFileExpansion expansion = Expand(file, NoVariables);

        Diagnostics.Assert("expanded text", expected, Text(expansion));
        Diagnostics.Assert("condition error set", true, expansion.ConditionError is not null);
        Assert.AreEqual(expected, Text(expansion));
        Assert.IsNotNull(expansion.ConditionError);
    }

    [TestMethod]
    public void Expand_TenThousandNestedIfBlocks_DropsTheirLinesAndKeepsTheRest()
    {
        const int Depth = 10_000;
        string file = string.Concat(Enumerable.Repeat("%if missing\n", Depth)) + "x\n" + string.Concat(Enumerable.Repeat("%endif\n", Depth)) + "y\n";

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(Encoding.Latin1.GetBytes(file), NoVariables, NoFeatures);

        Diagnostics.Assert("expanded text", "y\n", Text(expansion));
        Assert.AreEqual("y\n", Text(expansion));
        Assert.IsNull(expansion.ConditionError);
    }

    [TestMethod]
    public void Expand_IfNeverClosed_DropsToTheEndWithoutAnError()
    {
        UpstreamTestFileExpansion expansion = Expand("a\n%if missing\nb\n", NoVariables);

        Diagnostics.Assert("expanded text", "a\n", Text(expansion));
        Assert.AreEqual("a\n", Text(expansion));
        Assert.IsNull(expansion.ConditionError);
    }

    [TestMethod]
    public async Task Expand_SameFileOnManyTasksAtOnce_GivesTheSameExpansionEachTime()
    {
        byte[] file = "%if brotli\n%b64[%TESTNUMBER]b64%\n%else\n%repeat[3 x %hex[%41]hex%]%\n%endif\n"u8.ToArray();
        Dictionary<string, string> variables = new() { ["TESTNUMBER"] = "7" };

        string[] answers = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
            Text(UpstreamTestFileExpander.Expand(file, variables, index % 2 == 0 ? NoFeatures : new HashSet<string> { "brotli" })))));

        Diagnostics.Assert("answers", "AAA\n,Nw==\n", string.Join(",", answers.Distinct().Order(StringComparer.Ordinal)));
        Assert.IsTrue(answers.Where((_, index) => index % 2 == 0).All(answer => answer == "AAA\n"));
        Assert.IsTrue(answers.Where((_, index) => index % 2 == 1).All(answer => answer == "Nw==\n"));
    }
}
