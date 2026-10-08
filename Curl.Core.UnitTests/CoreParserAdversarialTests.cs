using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Attacks <c>Curl.Core.UnitLibrary</c>'s public parsers - <see cref="ByteRangeParser" />,
/// <see cref="RetryAfterHeader" />, <see cref="NoProxyMatcher" />, <see cref="ProxyUrlParser" />
/// and <see cref="UrlSchemeGuesser" /> - at their boundaries, with malformed input, in their
/// invalid partitions and under concurrent calls, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1497). The oracle is each type's
/// documented contract: the refusal it names, never an exception it does not promise.
/// </summary>
[TestClass]
public sealed class CoreParserAdversarialTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow("0-9223372036854775806")]
    [DataRow("9223372036854775807-9223372036854775807")]
    [DataRow("-9223372036854775807")]
    [DataRow("9223372036854775807-")]
    [DataRow("0-0")]
    [DataRow("-1")]
    public void ByteRangeParser_PositionsAtTheLongLimit_NameARange(string rangeText)
    {
        bool parsed = ByteRangeParser.TryParse(rangeText, out ByteRange? range);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(range);
    }

    [TestMethod]
    [DataRow("0-9223372036854775807")]
    [DataRow("9223372036854775808-1")]
    [DataRow("-0")]
    [DataRow("1-0")]
    [DataRow("")]
    [DataRow("1")]
    [DataRow("\u0661-\u0662")]
    [DataRow("\uff11-\uff12")]
    [DataRow("\0-1")]
    public void ByteRangeParser_TextNamingNoRange_IsRefusedWithoutThrowing(string rangeText)
    {
        bool parsed = ByteRangeParser.TryParse(rangeText, out ByteRange? range);

        Assert.IsFalse(parsed);
        Assert.IsNull(range);
    }

    [TestMethod]
    public void ByteRangeParser_OneHundredThousandRanges_ReadsOnlyTheFirst()
    {
        string rangeText = string.Join(',', Enumerable.Range(0, 100_000).Select(index => $"{index}-{index + 1}"));

        bool parsed = ByteRangeParser.TryParse(rangeText, out ByteRange? range);

        Assert.IsTrue(parsed);
        Assert.AreEqual(ByteRange.Bounded(0, 1), range);
    }

    [TestMethod]
    public void ByteRangeParser_TenThousandDigitPosition_CountsAsAbsent()
    {
        string rangeText = "5-" + new string('9', 10_000);

        bool parsed = ByteRangeParser.TryParse(rangeText, out ByteRange? range);

        Assert.IsTrue(parsed);
        Assert.AreEqual(ByteRange.FromOffset(5), range);
    }

    [TestMethod]
    public void ByteRangeParser_NullText_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ByteRangeParser.TryParse(null!, out _));

    [TestMethod]
    [DataRow("21599", 21599L)]
    [DataRow("21600", 21600L)]
    [DataRow("21601", 21600L)]
    [DataRow("9223372036854775807", 21600L)]
    [DataRow("9223372036854775808", 0L)]
    [DataRow("0", 0L)]
    [DataRow("-5", 0L)]
    [DataRow("+5", 0L)]
    [DataRow("", 0L)]
    [DataRow(" \t ", 0L)]
    [DataRow("\u0665", 0L)]
    [DataRow("\0" + "5", 0L)]
    public void RetryAfterHeader_SecondsAtAndPastTheLimits_StayBetweenZeroAndTheCap(string value, long expected)
    {
        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        Assert.AreEqual(expected, seconds);
    }

    [TestMethod]
    [DataRow(-1L, 0L)]
    [DataRow(0L, 0L)]
    [DataRow(1L, 1L)]
    [DataRow(21600L, 21600L)]
    [DataRow(21601L, 21600L)]
    [DataRow(3_000_000_000L, 21600L)]
    public void RetryAfterHeader_HttpDateAroundNow_WaitsUntilItCappedAtSixHours(long secondsFromNow, long expected)
    {
        string value = Now.AddSeconds(secondsFromNow).ToString("r", CultureInfo.InvariantCulture);

        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        Assert.AreEqual(expected, seconds);
    }

    [TestMethod]
    [DataRow("Wed, 32 Oct 2026 12:00:00 GMT")]
    [DataRow("Wed, 07 Oct 2026 25:61:61 GMT")]
    [DataRow("Wed, 07 Xyz 2026 12:00:00 GMT")]
    [DataRow("Wed, 07 Oct 99999999999 12:00:00 GMT")]
    [DataRow("Wed, 07 Oct")]
    public void RetryAfterHeader_MalformedDate_NeverThrowsAndStaysWithinTheCap(string value)
    {
        long seconds = RetryAfterHeader.ParseSeconds(value, Now);

        Assert.IsTrue(seconds is >= 0 and <= RetryAfterHeader.MaxSeconds, $"{value} gave {seconds}");
    }

    [TestMethod]
    public void RetryAfterHeader_NullValue_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => RetryAfterHeader.ParseSeconds(null!, Now));

    [TestMethod]
    [DataRow("example.com", "", false)]
    [DataRow("example.com", ",,,", false)]
    [DataRow("example.com", " \t , \t ", false)]
    [DataRow("example.com", "*,example.org", false)]
    [DataRow("example.com", "example.com.", true)]
    [DataRow("example.com.", "example.com", true)]
    [DataRow("sub.example.com", ".example.com", true)]
    [DataRow("badexample.com", "example.com", false)]
    [DataRow("EXAMPLE.COM", "example.com", true)]
    [DataRow("", "*", false)]
    [DataRow("::1", "::1", true)]
    [DataRow("10.1.2.3", "10.0.0.0/8", true)]
    [DataRow("11.1.2.3", "10.0.0.0/8", false)]
    [DataRow("10.1.2.3", "10.0.0.0/0", false)]
    [DataRow("10.0.0.0", "10.0.0.0/0", true)]
    [DataRow("10.1.2.3", "10.0.0.0/33", false)]
    [DataRow("10.1.2.3", "10.0.0.0/-1", false)]
    [DataRow("10.1.2.3", "10.0.0.0/", false)]
    [DataRow("10.1.2.3", "999.0.0.0/8", false)]
    [DataRow("::1", "::/129", false)]
    public void NoProxyMatcher_MalformedAndBoundaryLists_MatchOnlyWhatTheyName(string host, string noProxy, bool expected)
    {
        bool matches = NoProxyMatcher.Matches(host, noProxy);

        Assert.AreEqual(expected, matches);
    }

    [TestMethod]
    public void NoProxyMatcher_TenThousandEntriesBeforeTheMatch_FindsIt()
    {
        string noProxy = string.Join(',', Enumerable.Range(0, 10_000).Select(index => $"host{index}.test")) + ",target.test";

        string? entry = NoProxyMatcher.MatchingEntry("target.test", noProxy);

        Assert.AreEqual("target.test", entry);
    }

    [TestMethod]
    public void NoProxyMatcher_NullHost_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NoProxyMatcher.Matches(null!, "*"));

    [TestMethod]
    [DataRow("proxy.test:1", 1)]
    [DataRow("proxy.test:65535", 65535)]
    [DataRow("http://proxy.test:8080/", 8080)]
    [DataRow("socks5h://proxy.test", 1080)]
    public void ProxyUrlParser_PortsInsideTheRange_AreKept(string proxyText, int expectedPort)
    {
        bool parsed = ProxyUrlParser.TryParse(proxyText, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsTrue(parsed, failure?.ErrorMessage);
        Assert.AreEqual(expectedPort, proxy!.Port);
    }

    [TestMethod]
    [DataRow("proxy.test:0")]
    [DataRow("proxy.test:65536")]
    [DataRow("proxy.test:-1")]
    [DataRow("proxy.test:99999999999999999999")]
    [DataRow("proxy.test:8o")]
    [DataRow("[::1")]
    [DataRow("[zz::1]:80")]
    [DataRow("http://")]
    [DataRow("http:proxy.test")]
    [DataRow("user@:80@proxy.test")]
    [DataRow("pro xy.test")]
    [DataRow("pro\r\nxy.test")]
    [DataRow("proxy\0.test")]
    [DataRow("gopher://proxy.test")]
    [DataRow("%4")]
    [DataRow(":")]
    [DataRow("@")]
    public void ProxyUrlParser_MalformedText_FailsWithAProxyExitCodeWithoutThrowing(string proxyText)
    {
        bool parsed = ProxyUrlParser.TryParse(proxyText, out ProxyEndpoint? proxy, out TransferResult? failure);

        Assert.IsFalse(parsed, $"{proxyText} parsed as {proxy}");
        Assert.IsNotNull(failure);
        Assert.IsTrue(
            failure.ExitCode is CurlExitCode.CouldntResolveProxy or CurlExitCode.CouldntConnect,
            $"{proxyText} failed with {failure.ExitCode}");
    }

    [TestMethod]
    public void ProxyUrlParser_NullText_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ProxyUrlParser.TryParse(null!, out _, out _));

    [TestMethod]
    [DataRow("", "http://")]
    [DataRow("ftp.", "ftp://ftp.")]
    [DataRow("ftp", "http://ftp")]
    [DataRow("ftp.example.com", "ftp://ftp.example.com")]
    [DataRow("FTP.example.com", "ftp://FTP.example.com")]
    [DataRow("user@ftp.example.com", "ftp://user@ftp.example.com")]
    [DataRow("ftp.example.com@www.test", "http://ftp.example.com@www.test")]
    [DataRow("www.test/ftp.example.com", "http://www.test/ftp.example.com")]
    [DataRow("1http://x", "http://1http://x")]
    [DataRow("http:x", "http://http:x")]
    public void UrlSchemeGuesser_UrlsAtThePrefixAndSchemeEdges_GuessByTheHostOnly(string url, string expected)
    {
        string withScheme = UrlSchemeGuesser.AddGuessedScheme(url);

        Assert.AreEqual(expected, withScheme);
    }

    [TestMethod]
    [DataRow("h:/")]
    [DataRow("a+b.c-d://x")]
    [DataRow("HTTP://x")]
    public void UrlSchemeGuesser_AnySchemeShapedPrefix_CountsAsAScheme(string url) =>
        Assert.IsTrue(UrlSchemeGuesser.HasScheme(url));

    [TestMethod]
    public void UrlSchemeGuesser_NullUrl_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UrlSchemeGuesser.AddGuessedScheme(null!));

    [TestMethod]
    public async Task Parsers_CalledFromManyThreadsAtOnce_GiveTheSameAnswersAsOneThread()
    {
        string[] inputs = ["0-4", "-3", "5-", "3-1", "abc", "21601", "-1", "10.1.2.3", "example.com", "ftp.test"];
        string[] expected = inputs.Select(Answer).ToArray();

        string[][] answers = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => inputs.Select(Answer).ToArray())));

        foreach (string[] answer in answers)
        {
            CollectionAssert.AreEqual(expected, answer);
        }
    }

    private static string Answer(string input)
    {
        bool rangeParsed = ByteRangeParser.TryParse(input, out ByteRange? range);
        long seconds = RetryAfterHeader.ParseSeconds(input, Now);
        bool exempt = NoProxyMatcher.Matches(input, "10.0.0.0/8,.example.com,example.com");
        bool proxyParsed = ProxyUrlParser.TryParse(input, out ProxyEndpoint? proxy, out _);
        string url = UrlSchemeGuesser.AddGuessedScheme(input);
        return $"{rangeParsed}|{range}|{seconds}|{exempt}|{proxyParsed}|{proxy}|{url}";
    }
}
