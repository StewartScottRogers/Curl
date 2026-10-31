namespace Curl.Extension.StructuredFields.UnitTests;

/// <summary>The parsing algorithms of RFC 9651 §4.2, checked through the readable line each value gives.</summary>
[TestClass]
public sealed class StructuredFieldParserTests
{
    [TestMethod]
    public void ParseList_Section4_2_1_ReadsItemsAndInnerListsSeparatedByCommasAndOptionalWhitespace()
    {
        Assert.AreEqual("List of 3: Token sugar; Token tea; Inner List (Token a, Token b) [q=Integer 1]", List("  sugar, tea\t,\t( a  b );q=1  "));
        Assert.AreEqual("List of 0", List(string.Empty));
        Assert.AreEqual("List of 1: Inner List ()", List("()"));
    }

    [TestMethod]
    public void ParseList_Section4_2_1_FailsOnAMissingCommaATrailingCommaOrAnUnclosedInnerList()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseList("a b")), "unexpected 'b' at offset 2");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseList("a, ")), "a comma ends the value");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseList("(a b")), "an inner list is not closed");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseList("(a,b)")), "unexpected ','");
    }

    [TestMethod]
    public void ParseDictionary_Section4_2_2_ReadsMembersAndGivesABareKeyTheBooleanTrue()
    {
        Assert.AreEqual("Dictionary of 3: a=Boolean false; b=Boolean true [x=Token y]; c=Inner List (Integer 1)", Dictionary("a=?0, b;x=y, c=(1)"));
        Assert.AreEqual("Dictionary of 0", Dictionary(string.Empty));
    }

    [TestMethod]
    public void ParseDictionary_Section4_2_2_ARepeatedKeyKeepsItsPlaceAndTakesTheLastValue()
    {
        Assert.AreEqual("Dictionary of 2: a=Integer 3; b=Integer 2", Dictionary("a=1, b=2, a=3"));
    }

    [TestMethod]
    public void ParseDictionary_Section4_2_3_3_AKeyStartsWithALowercaseLetterOrStar()
    {
        Assert.AreEqual("Dictionary of 1: *a_b-c.d*9=Integer 1", Dictionary("*a_b-c.d*9=1"));
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseDictionary("A=1")), "unexpected 'A' at offset 0");
    }

    [TestMethod]
    public void ParseItem_Section4_2_3_2_ReadsParametersWithARepeatedKeyTakingTheLastValue()
    {
        Assert.AreEqual("Token a [q=Integer 2, r=Boolean true]", Item("a; q=1;r;q=2"));
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("a;")), "a key is missing");
    }

    [TestMethod]
    public void ParseItem_Section4_2_FailsOnNonAsciiTextOrAnythingAfterTheItem()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("\"é\"")), "not ASCII");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1 2")), "unexpected '2'");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1\t")), "unexpected '\t'");
        Assert.AreEqual("Integer 1", Item(" 1 "));
    }

    [TestMethod]
    public void ParseItem_Section4_2_3_1_FailsOnAMissingItemOrACharacterNoBareItemStartsWith()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(string.Empty)), "an item is missing");
        foreach (var unexpected in new[] { "!", "#", "(", "<", "[", "}", "\\" })
        {
            StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(unexpected)), "unexpected");
        }
    }

    [TestMethod]
    public void ParseItem_Section4_2_4_ReadsIntegersAndDecimals()
    {
        Assert.AreEqual("Integer -999999999999999", Item("-999999999999999"));
        Assert.AreEqual("Decimal -123456789012.5", Item("-123456789012.500"));
        Assert.AreEqual("Decimal 0.25", Item("0.25"));
    }

    [TestMethod]
    public void ParseItem_Section4_2_4_FailsOnNumbersPastTheirDigitLimits()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("-a")), "no digits");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1234567890123456")), "more than 15 digits");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1234567890123.1")), "decimal");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1.")), "decimal");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("1.1234")), "decimal");
    }

    [TestMethod]
    public void ParseItem_Section4_2_5_ReadsStringsWithEscapedQuotesAndBackslashes()
    {
        Assert.AreEqual("String \"say \\\"hi\\\" \\\\ \"", Item("\"say \\\"hi\\\" \\\\ \""));
    }

    [TestMethod]
    public void ParseItem_Section4_2_5_FailsOnABadEscapeAControlCharacterOrAnUnclosedString()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("\"\\a\"")), "escapes");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("\"a\\")), "ends in a backslash");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("\"a\tb\"")), "control character");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("\"a")), "not closed");
    }

    [TestMethod]
    public void ParseItem_Section4_2_6_ReadsTokensWithColonsAndSlashes()
    {
        Assert.AreEqual("Token *foo123/456:bar!#$%&'*+-.^_`|~", Item("*foo123/456:bar!#$%&'*+-.^_`|~"));
        Assert.AreEqual("Token Same-Origin", Item("Same-Origin"));
    }

    [TestMethod]
    public void ParseItem_Section4_2_7_ReadsByteSequencesWithOrWithoutPadding()
    {
        Assert.AreEqual("Byte Sequence 0x61 (1 bytes)", Item(":YQ==:"));
        Assert.AreEqual("Byte Sequence 0x6162 (2 bytes)", Item(":YWI:"));
        Assert.AreEqual("Byte Sequence 0x (0 bytes)", Item("::"));
    }

    [TestMethod]
    public void ParseItem_Section4_2_7_FailsOnAnUnclosedOrNonBase64ByteSequence()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(":YWI")), "not closed");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(":YW*I:")), "not base64");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(":Y:")), "not base64");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem(":Y=WI:")), "not base64");
    }

    [TestMethod]
    public void ParseItem_Section4_2_8_ReadsBooleansAndFailsOnAnyOtherDigit()
    {
        Assert.AreEqual("Boolean true", Item("?1"));
        Assert.AreEqual("Boolean false", Item("?0"));
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("?2")), "?0 or ?1");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("?")), "?0 or ?1");
    }

    [TestMethod]
    public void ParseItem_Section4_2_9_ReadsDatesAsIntegerSecondsSinceTheEpoch()
    {
        Assert.AreEqual("Date 2022-08-04T01:57:13Z", Item("@1659578233"));
        Assert.AreEqual("Date @999999999999999", Item("@999999999999999"));
        Assert.AreEqual("Date @-999999999999999", Item("@-999999999999999"));
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("@1.5")), "not an integer");
    }

    [TestMethod]
    public void ParseItem_Section4_2_10_ReadsDisplayStringsAsPercentEncodedUtf8()
    {
        Assert.AreEqual("Display String \"füü \\\"q\\\"\"", Item("%\"f%c3%bc%c3%bc %22q%22\""));
    }

    [TestMethod]
    public void ParseItem_Section4_2_10_FailsOnAMalformedDisplayString()
    {
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%a")), "does not start with");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%\"a")), "not closed");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%\"\t\"")), "control character");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%\"%C3%BC\"")), "lowercase hex");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%\"%c")), "lowercase hex");
        StringAssert.Contains(Failure(() => StructuredFieldParser.ParseItem("%\"%ff\"")), "not UTF-8");
    }

    private static string List(string text) => ReadableStructuredValue.OfList(StructuredFieldParser.ParseList(text));

    private static string Dictionary(string text) => ReadableStructuredValue.OfDictionary(StructuredFieldParser.ParseDictionary(text));

    private static string Item(string text) => ReadableStructuredValue.OfItem(StructuredFieldParser.ParseItem(text));

    private static string Failure(Action parse) => Assert.ThrowsExactly<FormatException>(parse).Message;
}
