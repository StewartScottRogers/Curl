using System.Globalization;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Attacks <see cref="CurlDateParser" /> at its boundaries, with days a month does not have,
/// in its invalid partitions and under concurrent calls, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1505). Every expected instant was
/// measured on curl 8.21.0 (the Schannel mingw build) on 2026-10-07 by passing the text to
/// <c>-z</c> through <c>Record-CurlExchange.ps1</c> and reading the
/// <c>If-Modified-Since</c> header curl sent; a refused text sent none.
/// </summary>
[TestClass]
public sealed class CurlDateParserAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("Tue, 19 Jan 2038 03:14:07 GMT", "2038-01-19T03:14:07Z")]
    [DataRow("Tue, 19 Jan 2038 03:14:08 GMT", "2038-01-19T03:14:08Z")]
    [DataRow("1 Jan 2030 23:59:60", "2030-01-02T00:00:00Z")]
    [DataRow("1 Jan 2030 +1400", "2029-12-31T10:00:00Z")]
    [DataRow("1 Jan 2030 -1400", "2030-01-01T14:00:00Z")]
    [DataRow("1 Jan 70", "2070-01-01T00:00:00Z")]
    [DataRow("1 Jan 69", "2069-01-01T00:00:00Z")]
    [DataRow("1 Jan 2030 00:00:00 Z", "2030-01-01T00:00:00Z")]
    public void TryParse_WithAFieldOnItsLimit_ReadsTheInstantCurlReads(string text, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRead(diagnostics, text, expected);
    }

    [TestMethod]
    [DataRow("29 Feb 2023", "2023-03-01T00:00:00Z")]
    [DataRow("31 Feb 2024", "2024-03-02T00:00:00Z")]
    [DataRow("31 Apr 2030", "2030-05-01T00:00:00Z")]
    public void TryParse_WithADayTheMonthDoesNotHave_RollsIntoTheNextMonthAsCurlDoes(string text, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRead(diagnostics, text, expected);
    }

    [TestMethod]
    [DataRow("1 Jan 2030 +1401")]
    [DataRow("20300101 120000")]
    public void TryParse_WithAZoneOrTimeOutsideCurlsForms_IsRefused(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("text", text);

        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("unix seconds", 0L, unixSeconds);
        Assert.IsFalse(parsed);
        Assert.AreEqual(0, unixSeconds);
    }

    [TestMethod]
    public void TryParse_WithEveryPrefixOfAValidDate_NeverThrows()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const string Text = "Sun, 06 Nov 1994 08:49:37 GMT";
        diagnostics.Arrange("text", Text);
        int calls = 0;

        for (int length = 0; length <= Text.Length; length++)
        {
            CurlDateParser.TryParse(Text[..length], out _);
            calls++;
        }

        diagnostics.Act("calls", calls);
        diagnostics.Assert("calls", Text.Length + 1, calls);
        Assert.AreEqual(Text.Length + 1, calls);
    }

    [TestMethod]
    public void TryParse_WithSeededRandomText_NeverThrows()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        const int Seed = 1505;
        const string Alphabet = "0123456789 :,+-ADEFGJMNOSTUVWZabcdefgjlmnoprstuvy\t\0é";
        diagnostics.Arrange("seed", Seed);
        Random random = new(Seed);
        int calls = 0;

        for (int sample = 0; sample < 5000; sample++)
        {
            char[] text = new char[random.Next(0, 48)];
            for (int index = 0; index < text.Length; index++)
            {
                text[index] = Alphabet[random.Next(Alphabet.Length)];
            }

            CurlDateParser.TryParse(new string(text), out _);
            calls++;
        }

        diagnostics.Act("calls", calls);
        diagnostics.Assert("calls", 5000, calls);
        Assert.AreEqual(5000, calls);
    }

    [TestMethod]
    public void TryParse_OnManyTasksAtOnce_GivesEachTheAnswerASingleCallGives()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] texts = ["Sun, 06 Nov 1994 08:49:37 GMT", "31 Feb 2024", "1 Jan 2030 +1401", "20300101"];
        diagnostics.Arrange("texts", string.Join(" | ", texts));
        (bool, long)[] expected = texts.Select(ParseOnce).ToArray();

        (bool, long)[] actual = Enumerable.Range(0, 400)
            .AsParallel()
            .Select(index => ParseOnce(texts[index % texts.Length]))
            .ToArray();

        int differences = actual
            .Select((answer, index) => (answer, index))
            .Count(pair => pair.answer != expected[pair.index % texts.Length]);
        diagnostics.Act("calls", actual.Length);
        diagnostics.Assert("answers differing from a single call", 0, differences);
        Assert.AreEqual(0, differences);
    }

    private static (bool, long) ParseOnce(string text)
    {
        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);
        return (parsed, unixSeconds);
    }

    private static void AssertRead(TestDiagnostics diagnostics, string text, string expected)
    {
        long expectedSeconds = DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture).ToUnixTimeSeconds();
        diagnostics.Arrange("text", text);
        diagnostics.Arrange("expected instant", expected);

        bool parsed = CurlDateParser.TryParse(text, out long unixSeconds);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("unix seconds", expectedSeconds, unixSeconds);
        Assert.IsTrue(parsed, text);
        Assert.AreEqual(expectedSeconds, unixSeconds, text);
    }
}
