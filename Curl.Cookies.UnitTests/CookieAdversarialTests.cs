using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cookies;

/// <summary>
/// Adversarial black-box tests (BL-1496, by <c>Documentation/Wiki/Adversarial-Testing.md</c>): the cookie library's
/// public surface attacked at its limits, with malformed headers and jar lines, in each invalid partition, and with
/// repeated, out-of-order and concurrent calls. The oracle is each member's documented contract, itself measured on
/// curl 8.21.0 by the tests beside these; nothing here reaches a network.
/// </summary>
[TestClass]
public sealed class CookieAdversarialTests
{
    private const long NowSeconds = 1_790_458_978;

    /// <summary>curl's 400-day cap for <see cref="NowSeconds"/>: <c>(Now + 34560000 + 30) / 60 * 60</c>.</summary>
    private const long CappedExpiry = 1_825_018_980;

    private const long FourHundredDays = 400L * 24 * 60 * 60;

    private const int FuzzSeed = 1496;

    private const int FuzzCases = 3000;

    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(NowSeconds);

    private static readonly CurlUrl Plain = CurlUrl.Parse("http://www.example.com/a/b");

    private static readonly CurlUrl Secure = CurlUrl.Parse("https://www.example.com/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // 1. Boundaries

    [TestMethod]
    [DataRow(4000, 96, true)]
    [DataRow(4000, 97, false)]
    [DataRow(1, 4095, true)]
    [DataRow(1, 4096, false)]
    [DataRow(4096, 0, true)]
    [DataRow(4097, 0, false)]
    public void Parse_NameAndValueAroundLongestNameAndValue_KeepsAtTheLimitAndRefusesOnePast(int nameLength, int valueLength, bool kept)
    {
        string header = new string('n', nameLength) + "=" + new string('v', valueLength);
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Diagnostics.Assert("kept", kept, cookie is not null);
        Assert.AreEqual(kept, cookie is not null);
        string? expectedRefusal = kept ? null : string.Create(CultureInfo.InvariantCulture, $"oversized cookie dropped, name/val {nameLength} + {valueLength} bytes");
        Assert.AreEqual(expectedRefusal, refusal);
    }

    [TestMethod]
    [DataRow(SetCookieParser.LongestHeaderValue - 1, true)]
    [DataRow(SetCookieParser.LongestHeaderValue, true)]
    [DataRow(SetCookieParser.LongestHeaderValue + 1, false)]
    public void Parse_HeaderAroundLongestHeaderValue_ReadsAtTheLimitAndDropsOnePastSilently(int headerLength, bool kept)
    {
        const string start = "a=b; Comment=";
        string header = start + new string('x', headerLength - start.Length);
        Diagnostics.Arrange("header length", header.Length);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Diagnostics.Assert("kept", kept, cookie is not null);
        Assert.AreEqual(kept, cookie is not null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    [DataRow("0", 1L)]
    [DataRow("-1", 1L)]
    [DataRow("-9223372036854775808", 1L)]
    [DataRow("+5", 1L)]
    [DataRow("abc", 1L)]
    [DataRow("1", NowSeconds + 1)]
    [DataRow("\"60", NowSeconds + 60)]
    [DataRow("60abc", NowSeconds + 60)]
    [DataRow("34560000", NowSeconds + FourHundredDays)]
    [DataRow("34560001", CappedExpiry)]
    [DataRow("9223372036854775807", CappedExpiry)]
    [DataRow("9223372036854775808", CappedExpiry)]
    [DataRow("99999999999999999999999999999999999999", CappedExpiry)]
    public void Parse_MaxAgeAtEveryBoundary_ExpiresAsDocumented(string maxAge, long expectedExpiry)
    {
        string header = "a=b; Max-Age=" + maxAge;
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        Diagnostics.Assert("expiry", expectedExpiry, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(expectedExpiry, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    public void Parse_MaxAgeOfLongMaxValueReceivedAtTheLastRepresentableSecond_CapsWithoutOverflowing()
    {
        DateTimeOffset lastSecond = DateTimeOffset.MaxValue.AddDays(-401);
        Diagnostics.Arrange("now", lastSecond.ToUnixTimeSeconds());

        Cookie? cookie = SetCookieParser.Parse("a=b; Max-Age=9223372036854775807", Plain, lastSecond);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        long cap = (lastSecond.ToUnixTimeSeconds() + FourHundredDays + 30) / 60 * 60;
        Diagnostics.Assert("expiry", cap, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(cap, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    [DataRow("Thu, 01 Jan 1970 00:00:00 GMT", 1L)]
    [DataRow("Thu, 01 Jan 1970 00:00:01 GMT", 1L)]
    [DataRow("Thu, 01 Jan 1970 00:00:02 GMT", 2L)]
    [DataRow("Fri, 31 Dec 9999 23:59:59 GMT", CappedExpiry)]
    [DataRow("not a date at all", 0L)]
    [DataRow("Wed, 32 Oct 2026 07:28:00 GMT", 0L)]
    [DataRow("Wed, 21 Oct 2026 25:28:00 GMT", 0L)]
    public void Parse_ExpiresFarPastFarFutureAndMalformed_ExpiresAsDocumented(string expires, long expectedExpiry)
    {
        string header = "a=b; Expires=" + expires;
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        Diagnostics.Assert("expiry", expectedExpiry, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(expectedExpiry, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    [DataRow("Wed, 21-Oct-26 07:28:00 GMT", 2026)]
    [DataRow("Thu, 21-Oct-99 07:28:00 GMT", 1999)]
    public void Parse_ExpiresWithTwoDigitYear_ReadsTheYearAsCurlDoes(string expires, int year)
    {
        string header = "a=b; Expires=" + expires;
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        long expected = new DateTimeOffset(year, 10, 21, 7, 28, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Diagnostics.Assert("expiry", expected, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(expected, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    [DataRow(79, true)]
    [DataRow(80, false)]
    public void Parse_ExpiresAroundEightyCharacters_ReadsShorterAndIgnoresAtEighty(int length, bool read)
    {
        const string date = "21 Oct 2026 07:28:00 GMT";
        string expires = "Wed," + new string(' ', length - date.Length - 4) + date;
        string header = "a=b; Expires=" + expires;
        Diagnostics.Arrange("Expires length", expires.Length);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        long expected = read ? new DateTimeOffset(2026, 10, 21, 7, 28, 0, TimeSpan.Zero).ToUnixTimeSeconds() : 0;
        Diagnostics.Assert("expiry", expected, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(expected, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    [DataRow(CookieStore.MostCookiesStoredPerResponse - 1, CookieStore.MostCookiesStoredPerResponse - 1)]
    [DataRow(CookieStore.MostCookiesStoredPerResponse, CookieStore.MostCookiesStoredPerResponse)]
    [DataRow(CookieStore.MostCookiesStoredPerResponse + 1, CookieStore.MostCookiesStoredPerResponse)]
    public void StoreFromResponse_HeadersAroundMostCookiesStoredPerResponse_StoresNoMoreThanTheLimit(int headers, int expectedStored)
    {
        CookieStore store = new();
        string[] setCookies = [.. Enumerable.Range(0, headers).Select(number => string.Create(CultureInfo.InvariantCulture, $"c{number}=v"))];
        Diagnostics.Arrange("Set-Cookie headers", headers);

        store.StoreFromResponse(Plain, setCookies, Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored", store.Cookies);

        Diagnostics.Assert("stored", expectedStored, store.Cookies.Count);
        Assert.AreEqual(expectedStored, store.Cookies.Count);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(CookieStore.MostCookiesStoredPerResponse - 1, CookieStore.MostCookiesStoredPerResponse)]
    [DataRow(CookieStore.MostCookiesStoredPerResponse, CookieStore.MostCookiesStoredPerResponse)]
    [DataRow(int.MaxValue, int.MaxValue)]
    public void StoreFromResponse_StoredCountAtEveryBoundary_ReturnsTheNextCountWithoutOverflow(int storedBefore, int expectedReturned)
    {
        CookieStore store = new();
        Diagnostics.Arrange("stored from response so far", storedBefore);

        int returned = store.StoreFromResponse(Plain, "a=b", storedBefore, Now, NoTransferEvents.Instance);
        Diagnostics.Act("returned", returned);

        Diagnostics.Assert("returned", expectedReturned, returned);
        Assert.AreEqual(expectedReturned, returned);
        Assert.AreEqual(returned != storedBefore ? 1 : 0, store.Cookies.Count);
    }

    [TestMethod]
    [DataRow(CookieStore.MostCookiesSent - 1, false)]
    [DataRow(CookieStore.MostCookiesSent, true)]
    [DataRow(CookieStore.MostCookiesSent + 1, true)]
    public void GetCookieHeader_CookiesAroundMostCookiesSent_SendsNoMoreThanTheLimitAndSaysSoAtIt(int cookieCount, bool saysMaximum)
    {
        CookieStore store = new();
        for (int number = 0; number < cookieCount; number++)
        {
            store.StoreFromResponse(Plain, string.Create(CultureInfo.InvariantCulture, $"c{number}=v"), 0, Now, NoTransferEvents.Instance);
        }

        RecordingTransferEvents events = new();
        Diagnostics.Arrange("stored cookies", store.Cookies.Count);

        string? header = store.GetCookieHeader(Plain, secure: false, Now, events);
        Diagnostics.Act("verbose lines", CookieTestDiagnostics.Shown(events.Info));

        int sent = header!.Split("; ").Length;
        Diagnostics.Assert("sent", Math.Min(cookieCount, CookieStore.MostCookiesSent), sent);
        Assert.AreEqual(Math.Min(cookieCount, CookieStore.MostCookiesSent), sent);
        Assert.AreEqual(saysMaximum, events.Info.Contains($"Included max number of cookies ({CookieStore.MostCookiesSent}) in request!"));
    }

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(1, false)]
    public void GetCookieHeader_HeaderAroundLongestCookieHeader_SendsAtTheLimitAndLeavesOutOnePast(int extra, bool secondSent)
    {
        CookieStore store = new();
        string first = "aa=" + new string('x', 4093);
        int secondValueLength = CookieStore.LongestCookieHeader - first.Length - "; b=".Length + extra;
        string second = "b=" + new string('y', secondValueLength);
        store.StoreFromResponse(Plain, [first, second], Now, NoTransferEvents.Instance);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("header length if both sent", first.Length + 2 + second.Length);

        string? header = store.GetCookieHeader(Plain, secure: false, Now, events);
        Diagnostics.Act("header length", header?.Length);
        Diagnostics.Act("verbose lines", CookieTestDiagnostics.Shown(events.Info));

        string expected = secondSent ? first + "; " + second : first;
        Diagnostics.Assert("header length", expected.Length, header?.Length);
        Assert.AreEqual(expected, header);
        Assert.AreEqual(!secondSent, events.Info.Contains("Restricted outgoing cookies due to header size, 'b' not sent"));
    }

    [TestMethod]
    [DataRow(NetscapeCookieFile.LongestLine, 2)]
    [DataRow(NetscapeCookieFile.LongestLine + 1, 0)]
    public void Read_LineAroundLongestLine_ReadsAtTheLimitAndStopsReadingOnePast(int lineLength, int expectedCookies)
    {
        const string start = "example.com\tFALSE\t/\tFALSE\t0\tlong\t";
        string longLine = start + new string('v', lineLength - start.Length);
        string file = longLine + "\nexample.com\tFALSE\t/\tFALSE\t0\tafter\tv\n";
        Diagnostics.Arrange("long line length", longLine.Length);

        IReadOnlyList<Cookie> cookies = NetscapeCookieFile.Read(new StringReader(file), Now);
        Diagnostics.ActCookies("read", cookies);

        Diagnostics.Assert("cookies read", expectedCookies, cookies.Count);
        Assert.AreEqual(expectedCookies, cookies.Count);
    }

    [TestMethod]
    public void Read_StreamedLineOfTwoMebibytesWithNoLineFeed_StopsReadingWithoutThrowing()
    {
        const int length = 2 * 1024 * 1024;
        using GeneratedReader reader = new(length);
        Diagnostics.Arrange("streamed line length", length);

        IReadOnlyList<Cookie> cookies = NetscapeCookieFile.Read(reader, Now);
        Diagnostics.ActCookies("read", cookies);

        Diagnostics.Assert("cookies read", 0, cookies.Count);
        Assert.AreEqual(0, cookies.Count);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("1", 1L)]
    [DataRow("12abc", 12L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    public void ParseLine_ExpiryFieldAtEveryBoundary_KeepsTheLeadingDigitsUncapped(string expiry, long expected)
    {
        string line = "example.com\tFALSE\t/\tFALSE\t" + expiry + "\tn\tv";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.Act("expiry", cookie?.ExpiresUnixSeconds);

        Diagnostics.Assert("expiry", expected, cookie?.ExpiresUnixSeconds);
        Assert.AreEqual(expected, cookie!.ExpiresUnixSeconds);
    }

    [TestMethod]
    [DataRow("9223372036854775808")]
    [DataRow("")]
    [DataRow("-1")]
    [DataRow("abc")]
    public void ParseLine_ExpiryFieldPastLongOrWithoutDigits_RefusesTheLine(string expiry)
    {
        string line = "example.com\tFALSE\t/\tFALSE\t" + expiry + "\tn\tv";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.IsNull(cookie);
    }

    // 2. Malformed input

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(";")]
    [DataRow(";;;;")]
    [DataRow("=")]
    [DataRow("=v")]
    [DataRow("   =v")]
    [DataRow("name")]
    [DataRow("\t")]
    public void Parse_HeaderWithNoUsableFirstPart_RefusesAsInvalidCookie(string header)
    {
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Assert.IsNull(cookie);
        Assert.AreEqual("invalid cookie, dropped", refusal);
    }

    [TestMethod]
    [DataRow("a=b\r\nEvil: 1", "invalid octets in value, cookie dropped")]
    [DataRow("a=b\nEvil: 1", "invalid octets in value, cookie dropped")]
    [DataRow("a=b\0c", "invalid octets in value, cookie dropped")]
    [DataRow("a=b\u007Fc", "invalid octets in value, cookie dropped")]
    [DataRow("a=b c\td", "invalid octets in value, cookie dropped")]
    [DataRow("a\r\nb=c", "invalid octets in name, cookie dropped")]
    [DataRow("a\0=c", "invalid octets in name, cookie dropped")]
    [DataRow("a=b; Path=/x\r\nEvil: 1", "invalid octets in value, cookie dropped")]
    [DataRow("a=b; Domain=www.example.com\0.evil", "invalid octets in value, cookie dropped")]
    [DataRow("a=b; Ev\u0001il=1", "invalid octets in name, cookie dropped")]
    public void Parse_ControlCharactersInjected_RefusesWithTheOctetsLine(string header, string expectedRefusal)
    {
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Assert.IsNull(cookie);
        Assert.AreEqual(expectedRefusal, refusal);
    }

    [TestMethod]
    public void Parse_SeededRandomHeaders_NeverThrowAndKeepOnlyCookiesThatHonourTheContract()
    {
        Random random = new(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        Diagnostics.Arrange("cases", FuzzCases);
        CurlUrl[] urls = [Plain, Secure, CurlUrl.Parse("http://127.0.0.1/x/"), CurlUrl.Parse("http://[::1]/")];
        int kept = 0;

        for (int number = 0; number < FuzzCases; number++)
        {
            string header = RandomHeader(random);
            CurlUrl url = urls[number % urls.Length];
            Cookie? cookie;
            Cookie? fromFile;
            try
            {
                cookie = SetCookieParser.Parse(header, url, Now, out _);
                fromFile = SetCookieParser.ParseFromCookieFile(header, Now, out _);
                _ = NetscapeCookieFile.ParseLine(header, Now, out _);
            }
            catch (Exception exception)
            {
                Diagnostics.ArrangeText(string.Create(CultureInfo.InvariantCulture, $"case {number} from {url.OriginalString}"), header);
                throw new AssertFailedException(string.Create(CultureInfo.InvariantCulture, $"seed {FuzzSeed}, case {number}: {exception}"));
            }

            foreach (Cookie? parsed in new[] { cookie, fromFile })
            {
                if (parsed is null)
                {
                    continue;
                }

                kept++;
                string problem = ContractBreach(parsed);
                if (problem.Length > 0)
                {
                    Diagnostics.ArrangeText(string.Create(CultureInfo.InvariantCulture, $"case {number} from {url.OriginalString}"), header);
                    Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"seed {FuzzSeed}, case {number}: {problem}"));
                }
            }
        }

        Diagnostics.Act("cookies kept", kept);
        Assert.IsTrue(kept > 0, "the generator never produced an acceptable cookie");
    }

    [TestMethod]
    [DataRow("example.com\tFALSE\t/\tFALSE\t0")]
    [DataRow("example.com\tFALSE\t/")]
    [DataRow("example.com")]
    [DataRow("")]
    [DataRow("\t\t\t\t\t\t\t")]
    [DataRow("example.com\tFALSE\t/\tFALSE\t0\tn\tv\textra")]
    [DataRow("#HttpOnly_")]
    [DataRow("#httponly_example.com\tFALSE\t/\tFALSE\t0\tn\tv")]
    [DataRow("# example.com\tFALSE\t/\tFALSE\t0\tn\tv")]
    [DataRow("\rexample.com\tFALSE\t/\tFALSE\t0\tn\tv")]
    [DataRow("example.com\tFALSE\t/\tFALSE\t0\tn\u0001\tv")]
    [DataRow("example.com\tFALSE\t/\tFALSE\t0\tn\tv\u007F")]
    public void ParseLine_MalformedJarLine_RefusesWithoutThrowing(string line)
    {
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now, out string? refusal);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.IsNull(cookie);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void ParseLine_SixFields_ReadsAnEmptyValue()
    {
        const string line = "example.com\tFALSE\t/\tFALSE\t0\tn";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.AreEqual(string.Empty, cookie!.Value);
        Assert.AreEqual("n", cookie.Name);
    }

    [TestMethod]
    public void ParseLine_CarriageReturnInsideTheValue_EndsTheLineThere()
    {
        const string line = "example.com\tFALSE\t/\tFALSE\t0\tn\tv1\rtail";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.AreEqual("v1", cookie!.Value);
    }

    [TestMethod]
    public void ParseLine_HttpOnlyPrefix_KeepsTheCookieAsHttpOnlyAndWritesItBack()
    {
        const string line = "#HttpOnly_.example.com\tTRUE\t/\tFALSE\t0\tn\tv";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.IsTrue(cookie!.IsHttpOnly);
        Assert.AreEqual("example.com", cookie.Domain);
        string written = NetscapeCookieFile.FormatLine(cookie);
        Diagnostics.AssertText("written", line, written);
        Assert.AreEqual(line, written);
    }

    [TestMethod]
    public void WriteAndRead_SeededRandomJarLines_ReadBackTheSameCookies()
    {
        Random random = new(FuzzSeed);
        Diagnostics.Arrange("seed", FuzzSeed);
        List<Cookie> original = [];
        for (int number = 0; number < 500; number++)
        {
            Cookie? cookie = NetscapeCookieFile.ParseLine(RandomJarLine(random, number), Now);
            if (cookie is not null)
            {
                original.Add(cookie);
            }
        }

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        NetscapeCookieFile.Write(writer, original);
        IReadOnlyList<Cookie> readBack = NetscapeCookieFile.Read(new StringReader(writer.ToString()), Now);
        Diagnostics.Act("written and read back", readBack.Count);

        Diagnostics.AssertTexts("cookies", original.Select(NetscapeCookieFile.FormatLine), readBack.Select(NetscapeCookieFile.FormatLine));
        Assert.IsTrue(original.Count > 100, "the generator produced too few cookies");
        CollectionAssert.AreEqual(original, readBack.ToList());
    }

    // 3. Invalid partitions

    [TestMethod]
    [DataRow("http://www.example.com/", "a=1; Domain=example.com", true)]
    [DataRow("http://www.example.com/", "a=1; Domain=.example.com", true)]
    [DataRow("http://www.example.com/", "a=1; Domain=EXAMPLE.COM", true)]
    [DataRow("http://WWW.EXAMPLE.COM/", "a=1; Domain=example.com", true)]
    [DataRow("http://www.example.com/", "a=1; Domain=..example.com", false)]
    [DataRow("http://www.example.com/", "a=1; Domain=.", false)]
    [DataRow("http://www.example.com/", "a=1; Domain=xample.com", false)]
    [DataRow("http://www.example.com/", "a=1; Domain=www.example.com.evil", false)]
    [DataRow("http://www.example.com/", "a=1; Domain=sub.www.example.com", false)]
    [DataRow("http://127.0.0.1/", "a=1; Domain=127.0.0.1", true)]
    [DataRow("http://127.0.0.1/", "a=1; Domain=.127.0.0.1", true)]
    [DataRow("http://127.0.0.1/", "a=1; Domain=0.0.1", false)]
    [DataRow("http://10.0.0.1/", "a=1; Domain=10.0.0.10", false)]
    [DataRow("http://[::1]/", "a=1; Domain=::1", true)]
    public void Parse_DomainInEveryPartition_KeepsOnlyTheHostOrAParent(string url, string header, bool kept)
    {
        Diagnostics.ArrangeText(url, header);

        Cookie? cookie = SetCookieParser.Parse(header, CurlUrl.Parse(url), Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Diagnostics.Assert("kept", kept, cookie is not null);
        Assert.AreEqual(kept, cookie is not null);
        Assert.AreEqual(kept, refusal is null);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1/", "a=1; Domain=127.0.0.1")]
    [DataRow("http://[::1]/", "a=1; Domain=::1")]
    public void Parse_DomainOfAnIpAddress_StaysHostOnly(string url, string header)
    {
        Diagnostics.ArrangeText(url, header);

        Cookie? cookie = SetCookieParser.Parse(header, CurlUrl.Parse(url), Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.IsFalse(cookie!.IncludesSubdomains);
    }

    [TestMethod]
    public void GetCookieHeader_DomainSetInADifferentCase_IsSentToTheHost()
    {
        CookieStore store = new();
        store.StoreFromResponse(Plain, ["a=1; Domain=EXAMPLE.COM; Path=/"], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored", store.Cookies);

        string? header = store.GetCookieHeader(CurlUrl.Parse("http://other.Example.Com/"), secure: false, Now);

        Diagnostics.AssertText("Cookie header", "a=1", header);
        Assert.AreEqual("a=1", header);
    }

    [TestMethod]
    [DataRow("http://www.example.com/", "a=1; Domain=com", "cookie 'a' dropped, domain 'www.example.com' must not set cookies for 'com'")]
    [DataRow("http://www.example.co.uk/", "a=1; Domain=co.uk", "cookie 'a' dropped, domain 'www.example.co.uk' must not set cookies for 'co.uk'")]
    [DataRow("http://www.example.co.uk/", "a=1; Domain=.CO.UK", "cookie 'a' dropped, domain 'www.example.co.uk' must not set cookies for 'CO.UK'")]
    public void StoreFromResponse_DomainThatIsAPublicSuffix_DropsTheCookieAndSaysWhy(string url, string header, string expectedLine)
    {
        CookieStore store = new();
        RecordingTransferEvents events = new();
        Diagnostics.ArrangeText(url, header);

        store.StoreFromResponse(CurlUrl.Parse(url), [header], Now, events);
        Diagnostics.Act("verbose lines", CookieTestDiagnostics.Shown(events.Info));

        Assert.AreEqual(0, store.Cookies.Count);
        CollectionAssert.Contains(events.Info, expectedLine);
    }

    [TestMethod]
    [DataRow("http://co.uk/", "a=1; Domain=co.uk")]
    [DataRow("http://www.example.co.uk/", "a=1; Domain=example.co.uk")]
    public void StoreFromResponse_DomainThatIsTheHostOrLongerThanItsPublicSuffix_KeepsTheCookie(string url, string header)
    {
        CookieStore store = new();
        Diagnostics.ArrangeText(url, header);

        store.StoreFromResponse(CurlUrl.Parse(url), [header], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored", store.Cookies);

        Assert.AreEqual(1, store.Cookies.Count);
    }

    [TestMethod]
    [DataRow("https://www.example.com/", "__Secure-a=1; Secure", true)]
    [DataRow("https://www.example.com/", "__Secure-a=1", false)]
    [DataRow("https://www.example.com/", "__Secure-a=1; Secure=yes", false)]
    [DataRow("https://www.example.com/", "__secure-a=1", true)]
    [DataRow("https://www.example.com/", "__Host-a=1; Secure; Path=/", true)]
    [DataRow("https://www.example.com/a/b", "__Host-a=1; Secure", false)]
    [DataRow("https://www.example.com/", "__Host-a=1; Path=/", false)]
    [DataRow("https://www.example.com/", "__Host-a=1; Secure; Path=/x", false)]
    [DataRow("https://www.example.com/", "__Host-a=1; Secure; Path=/; Domain=example.com", false)]
    [DataRow("https://www.example.com/", "__Host-a=1; Secure; Path=/; Domain=www.example.com", false)]
    [DataRow("https://127.0.0.1/", "__Host-a=1; Secure; Path=/; Domain=127.0.0.1", true)]
    [DataRow("https://www.example.com/", "__host-a=1", true)]
    public void Parse_NamePrefixInEveryPartition_KeepsOnlyCookiesThatSatisfyIt(string url, string header, bool kept)
    {
        Diagnostics.ArrangeText(url, header);

        Cookie? cookie = SetCookieParser.Parse(header, CurlUrl.Parse(url), Now, out string? refusal);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Diagnostics.Assert("kept", kept, cookie is not null);
        Assert.AreEqual(kept, cookie is not null);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    [DataRow("__Secure-a\tFALSE", false)]
    [DataRow("__Secure-a\tTRUE", true)]
    [DataRow("__Host-a\tTRUE", true)]
    [DataRow("__Host-a\tFALSE", false)]
    public void ParseLine_NamePrefixOnAJarLine_KeepsOnlyLinesThatSatisfyIt(string nameAndSecure, bool kept)
    {
        string[] parts = nameAndSecure.Split('\t');
        string line = "www.example.com\tFALSE\t/\t" + parts[1] + "\t0\t" + parts[0] + "\tv";
        Diagnostics.ArrangeText("line", line);

        Cookie? cookie = NetscapeCookieFile.ParseLine(line, Now);
        Diagnostics.ActText("cookie", cookie?.ToString());

        Assert.AreEqual(kept, cookie is not null);
    }

    [TestMethod]
    [DataRow("http://www.example.com/", "a=1; Secure")]
    [DataRow("http://www.example.com/", "a=1; SECURE")]
    [DataRow("ftp://www.example.com/", "a=1; Secure")]
    public void Parse_SecureFromAnOriginThatIsNotSecure_RefusesTheCookie(string url, string header)
    {
        Diagnostics.ArrangeText(url, header);

        Cookie? cookie = SetCookieParser.Parse(header, CurlUrl.Parse(url), Now, out string? refusal);
        Diagnostics.ActText("refusal", refusal);

        Assert.IsNull(cookie);
        Assert.AreEqual("skipped cookie because not 'secure'", refusal);
    }

    [TestMethod]
    [DataRow("a=1; Path=/x/../y", "/x/../y")]
    [DataRow("a=1; Path=//", "/")]
    [DataRow("a=1; Path=/%2F", "/%2F")]
    [DataRow("a=1; Path=\"\"", "/")]
    [DataRow("a=1; Path=\\x", "/")]
    [DataRow("a=1; Path=http://evil/", "/")]
    public void Parse_PathEdgeForms_SanitizesAsDocumented(string header, string expectedPath)
    {
        Diagnostics.ArrangeText("header", header);

        Cookie? cookie = SetCookieParser.Parse(header, Plain, Now);
        Diagnostics.ActText("path", cookie?.Path);

        Diagnostics.AssertText("path", expectedPath, cookie?.Path);
        Assert.AreEqual(expectedPath, cookie!.Path);
    }

    // 4. State and concurrency

    [TestMethod]
    public void StoreFromResponse_AfterARefusedHeader_StoresTheNextOneUnaffected()
    {
        CookieStore store = new();
        RecordingTransferEvents events = new();

        int afterRefusal = store.StoreFromResponse(Plain, "a=b\r\nEvil: 1", 0, Now, events);
        int afterValid = store.StoreFromResponse(Plain, "a=c", afterRefusal, Now, events);
        Diagnostics.Act("verbose lines", CookieTestDiagnostics.Shown(events.Info));

        Assert.AreEqual(0, afterRefusal);
        Assert.AreEqual(1, afterValid);
        Assert.AreEqual("a=c", store.GetCookieHeader(Plain, secure: false, Now));
    }

    [TestMethod]
    public void StoreFromResponse_SameCookieManyTimes_KeepsOneAndReportsReplaced()
    {
        CookieStore store = new();
        RecordingTransferEvents events = new();

        for (int number = 0; number < 100; number++)
        {
            store.StoreFromResponse(Plain, string.Create(CultureInfo.InvariantCulture, $"a={number}"), 0, Now, events);
        }

        Diagnostics.ActCookies("stored", store.Cookies);

        Assert.AreEqual(1, store.Cookies.Count);
        Assert.AreEqual("a=99", store.GetCookieHeader(Plain, secure: false, Now));
        Assert.AreEqual(99, events.Info.Count(line => line.StartsWith("Replaced cookie a=", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void GetCookieHeader_ClockMovingBackwardsAndForwards_NeverResurrectsAnExpiredCookie()
    {
        CookieStore store = new();
        store.StoreFromResponse(Plain, ["a=1; Max-Age=10"], Now, NoTransferEvents.Instance);

        string? before = store.GetCookieHeader(Plain, secure: false, Now.AddSeconds(-1000));
        string? atExpiry = store.GetCookieHeader(Plain, secure: false, Now.AddSeconds(10));
        string? after = store.GetCookieHeader(Plain, secure: false, Now.AddSeconds(11));
        string? backAgain = store.GetCookieHeader(Plain, secure: false, Now);
        Diagnostics.Act("headers at -1000, +10, +11, +0", CookieTestDiagnostics.Shown([before ?? "(null)", atExpiry ?? "(null)", after ?? "(null)", backAgain ?? "(null)"]));

        Assert.AreEqual("a=1", before);
        Assert.AreEqual("a=1", atExpiry);
        Assert.IsNull(after);
        Assert.IsNull(backAgain);
    }

    [TestMethod]
    public void LoadCookieFile_SameFileTwice_KeepsOneCopyOfEachCookie()
    {
        CookieStore store = new();
        const string file = "www.example.com\tFALSE\t/\tFALSE\t0\ta\t1\nwww.example.com\tFALSE\t/\tFALSE\t0\tb\t2\n";

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now);
        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now);
        Diagnostics.ActCookies("stored", store.Cookies);

        Assert.AreEqual(2, store.Cookies.Count);
    }

    [TestMethod]
    public async Task EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother()
    {
        const int callers = 64;
        CookieStore concurrent = new();
        CookieStore sequential = new();
        ConcurrentBag<Exception> failures = [];
        Diagnostics.Arrange("callers", callers);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, callers),
            (number, cancellationToken) =>
            {
                try
                {
                    CallEveryMember(concurrent, number);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                return ValueTask.CompletedTask;
            });
        for (int number = 0; number < callers; number++)
        {
            CallEveryMember(sequential, number);
        }

        // Every caller replaces "shared", so its final value is whichever caller wrote last: the
        // thread scheduler's choice, not the store's. Compare it as "some caller's whole number".
        string[] expected = [.. sequential.Cookies.Select(LineWithSharedValueMasked).Order(StringComparer.Ordinal)];
        string[] actual = [.. concurrent.Cookies.Select(LineWithSharedValueMasked).Order(StringComparer.Ordinal)];
        string sharedValue = concurrent.Cookies.Single(cookie => cookie.Name == "shared").Value;
        Diagnostics.AssertTexts("cookies, sorted, shared value masked", expected, actual);
        Diagnostics.Act("shared value, last writer's", sharedValue);
        Assert.IsTrue(failures.IsEmpty, string.Join(Environment.NewLine, failures));
        CollectionAssert.AreEqual(expected, actual);
        Assert.IsTrue(int.TryParse(sharedValue, NumberStyles.None, CultureInfo.InvariantCulture, out int writer) && writer < callers, "shared=" + sharedValue + " was written by no caller");
    }

    private static string LineWithSharedValueMasked(Cookie cookie) =>
        NetscapeCookieFile.FormatLine(cookie.Name == "shared" ? cookie with { Value = "(any caller)" } : cookie);

    private static void CallEveryMember(CookieStore store, int number)
    {
        string loaded = string.Create(CultureInfo.InvariantCulture, $"www.example.com\tFALSE\t/\tFALSE\t0\tfile{number}\tv\n");
        store.LoadCookieFile(new StringReader(loaded), discardSessionCookies: false, Now);
        store.StoreFromResponse(Plain, [string.Create(CultureInfo.InvariantCulture, $"resp{number}=v"), "shared=" + number.ToString(CultureInfo.InvariantCulture)], Now, NoTransferEvents.Instance);
        _ = store.GetCookieHeader(Plain, secure: false, Now);
        using StringWriter jar = new(CultureInfo.InvariantCulture);
        store.WriteCookieJar(jar, Now);
        Cookie shared = store.Cookies.Single(cookie => cookie.Name == "shared");
        if (!shared.Value.All(char.IsAsciiDigit))
        {
            throw new InvalidOperationException("torn cookie value " + shared.Value);
        }
    }

    /// <summary>The first way <paramref name="cookie"/> breaks the parser's documented contract, or empty.</summary>
    private static string ContractBreach(Cookie cookie)
    {
        if (cookie.Name.Length == 0)
        {
            return "empty name";
        }

        if (cookie.Name.Length + cookie.Value.Length > SetCookieParser.LongestNameAndValue)
        {
            return "name and value past the limit";
        }

        if (cookie.Name.Any(IsRefusedControl) || cookie.Value.Any(IsRefusedControl) || cookie.Value.Contains('\t', StringComparison.Ordinal))
        {
            return "control character kept in the name or value";
        }

        return cookie.Path.Length > 0 && !cookie.Path.StartsWith('/') ? "path not starting with /" : string.Empty;
    }

    private static bool IsRefusedControl(char character) => character is (< ' ' and not '\t') or '\u007F';

    private static string RandomHeader(Random random)
    {
        string[] pieces =
        [
            "a", "name", "=", "==", ";", "; ", " ", "\t", "\"", ".", "/", "\\", "v", "Path=", "Domain=", "Max-Age=", "Expires=",
            "Secure", "HttpOnly", "__Secure-", "__Host-", "example.com", ".example.com", "com", "-1", "0", "99999999999999999999",
            "Wed, 21 Oct 2026 07:28:00 GMT", "\r", "\n", "\0", "\u007F", "é", "￿", "\uD800", "%0d%0a",
        ];
        StringBuilder header = new();
        int count = random.Next(0, 24);
        for (int piece = 0; piece < count; piece++)
        {
            header.Append(pieces[random.Next(pieces.Length)]);
        }

        if (random.Next(20) == 0)
        {
            header.Append('x', random.Next(4000, 5100));
        }

        return header.ToString();
    }

    private static string RandomJarLine(Random random, int number)
    {
        string[] domains = ["example.com", ".example.com", "127.0.0.1", "::1", "a.b.c.example", string.Empty];
        string[] flags = ["TRUE", "FALSE", "T", "tru", "F", "false"];
        string[] paths = ["/", "/a", "/a/", "\"/q\"", "x", "/a b", "/%2F"];
        string[] values = [string.Empty, "v", "a b", "\"quoted\"", "=", ";", "é", new string('x', 300)];
        string prefix = random.Next(4) == 0 ? "#HttpOnly_" : string.Empty;
        return string.Join(
            '\t',
            prefix + domains[random.Next(domains.Length)],
            flags[random.Next(flags.Length)],
            paths[random.Next(paths.Length)],
            flags[random.Next(flags.Length)],
            random.Next(3) == 0 ? "0" : random.NextInt64(1, long.MaxValue).ToString(CultureInfo.InvariantCulture),
            string.Create(CultureInfo.InvariantCulture, $"n{number}"),
            values[random.Next(values.Length)]);
    }

    /// <summary>A reader that makes up <c>x</c> characters as they are read, holding none of them.</summary>
    private sealed class GeneratedReader(int length) : TextReader
    {
        private int remaining = length;

        public override int Peek() => remaining > 0 ? 'x' : -1;

        public override int Read()
        {
            if (remaining == 0)
            {
                return -1;
            }

            remaining--;
            return 'x';
        }
    }
}
