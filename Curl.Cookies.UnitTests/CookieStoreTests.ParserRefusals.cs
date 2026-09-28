using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins the <c>-v</c> line <see cref="CookieStore.StoreFromResponse"/> reports for a <c>Set-Cookie</c> header
/// <see cref="SetCookieParser"/> refuses (BL-443). Measured on curl 8.21.0 (mingw) on 2026-09-27:
/// <c>Record-CurlExchange.ps1</c> served <c>Set-Cookie: &lt;header&gt;</c> to
/// <c>curl -s -v --resolve www.example.co.uk:&lt;port&gt;:127.0.0.1 -c - http://www.example.co.uk:&lt;port&gt;/</c>,
/// and each expected line is the text curl wrote to standard error after <c>* </c>. BL-443's Notes list the runs.
/// </summary>
public sealed partial class CookieStoreTests
{
    private const string InvalidCookie = "invalid cookie, dropped";

    private const string InvalidOctetsInName = "invalid octets in name, cookie dropped";

    private const string InvalidOctetsInValue = "invalid octets in value, cookie dropped";

    private const string NotSecure = "skipped cookie because not 'secure'";

    private const string BadTailmatch = "skipped cookie with bad tailmatch domain: ";

    [TestMethod]
    [DataRow("noequals", InvalidCookie)]
    [DataRow("=emptyname", InvalidCookie)]
    [DataRow(" =v", InvalidCookie, DisplayName = "A name that is only a blank")]
    [DataRow("n\tb=v; Path=/", InvalidCookie, DisplayName = "A tab ends the name before its =")]
    [DataRow("n\u0001a=v; Path=/", InvalidOctetsInName)]
    [DataRow("\u0001n=v", InvalidOctetsInName)]
    [DataRow("n\u0001", InvalidOctetsInName, DisplayName = "Before the missing = is noticed")]
    [DataRow("\u0001n=a\u0001", InvalidOctetsInName, DisplayName = "The name before the value")]
    [DataRow("n\u0001=a\tb", InvalidOctetsInName, DisplayName = "The name before a tab in the value")]
    [DataRow("n=v; Pa\u0001th=/", InvalidOctetsInName)]
    [DataRow("n=v; Foo\u0002=bar", InvalidOctetsInName, DisplayName = "In an unknown attribute")]
    [DataRow("n=v; Sec\u0001ure", InvalidOctetsInName, DisplayName = "In a flag")]
    [DataRow("t=a\tb; Path=/", InvalidOctetsInValue)]
    [DataRow("c=a\u0001b; Path=/", InvalidOctetsInValue)]
    [DataRow("n=a\u007Fb", InvalidOctetsInValue, DisplayName = "DEL")]
    [DataRow("=a\u0001", InvalidOctetsInValue, DisplayName = "Before the empty name is noticed")]
    [DataRow("n=v; Path=/a\tb", InvalidOctetsInValue)]
    [DataRow("n=v; Path=/a\u0001b", InvalidOctetsInValue)]
    [DataRow("n=v; =\u0001", InvalidOctetsInValue, DisplayName = "An attribute without a name")]
    [DataRow("n=v; Path=/a\u0001b; Secure", InvalidOctetsInValue, DisplayName = "Before a later Secure")]
    [DataRow("n=a\tb; X=\u0001", InvalidOctetsInValue, DisplayName = "The first refusal wins")]
    [DataRow("n4=v; Path=/; Secure", NotSecure)]
    [DataRow("n=v; Secure\t; Path=/", NotSecure, DisplayName = "Secure ended by a tab")]
    [DataRow("n=v; Secure; Path=/a\u0001b", NotSecure, DisplayName = "Before a later control character")]
    [DataRow("n3=v; Path=/; Domain=other.test", BadTailmatch + "other.test")]
    [DataRow("n=v; Path=/; Domain=.other.test", BadTailmatch + "other.test", DisplayName = "Without its leading dot")]
    [DataRow("n=v; Path=/; Domain=OTHER.Test", BadTailmatch + "OTHER.Test", DisplayName = "In the case sent")]
    [DataRow("n=v; Domain=\".other.test\"", BadTailmatch + "\".other.test\"", DisplayName = "Quotes kept")]
    [DataRow("n=v; Domain=other.test ; Path=/", BadTailmatch + "other.test ; Path=/", DisplayName = "The rest of the header")]
    [DataRow("n=v; Domain=  .other.test;Path=/", BadTailmatch + "other.test;Path=/", DisplayName = "Leading blanks and dot left out")]
    [DataRow("n=v; Domain=other.test   ", BadTailmatch + "other.test   ", DisplayName = "Trailing blanks kept")]
    [DataRow("n=v; Domain=other.test\t; Path=/", BadTailmatch + "other.test\t; Path=/", DisplayName = "A trailing tab kept")]
    [DataRow("n=v; Domain=other.test; X=a\u0001", BadTailmatch + "other.test; X=a\u0001", DisplayName = "Before a later control character, which is printed")]
    [DataRow("n=v; Domain=www.example.co.uk; Domain=x.test; Path=/", BadTailmatch + "x.test; Path=/", DisplayName = "The Domain that failed")]
    public void StoreFromResponse_ParserRefusesTheHeader_ReportsCurlsLine(string header, string expectedLine) =>
        AssertReportsOnly(CoUk, header, expectedLine);

    [TestMethod]
    public void StoreFromResponse_DomainIsAnotherIpAddress_ReportsBadTailmatch() =>
        AssertReportsOnly(Loopback, "i=1; Domain=127.0.0.2", BadTailmatch + "127.0.0.2");

    [TestMethod]
    public void StoreFromResponse_LongNameAndValueWithAControlCharacter_ReportsInvalidOctetsInValue() =>
        AssertReportsOnly(CoUk, $"{new string('a', 4000)}={new string('b', 200)}\u0001", InvalidOctetsInValue);

    [TestMethod]
    [DataRow("=", DisplayName = "An empty name")]
    [DataRow("", DisplayName = "No =")]
    public void StoreFromResponse_LongHeaderWithoutAName_ReportsInvalidCookie(string start) =>
        AssertReportsOnly(CoUk, start + new string('a', 4200), InvalidCookie);

    [TestMethod]
    [DataRow("__Secure-a=1; Path=/")]
    [DataRow("__Host-b=1; Path=/")]
    public void StoreFromResponse_NamePrefixNotSatisfied_ReportsNothing(string header) =>
        AssertReportsNothing(header);

    [TestMethod]
    public void StoreFromResponse_NameAndValueTooLong_ReportsNothing() =>
        AssertReportsNothing($"{new string('a', 4000)}={new string('b', 200)}");

    [TestMethod]
    public void StoreFromResponse_HeaderTooLong_ReportsNothing() =>
        AssertReportsNothing($"n={new string('b', SetCookieParser.LongestHeaderValue)}\u0001");

    /// <summary>
    /// Measured: a part that ends at a tab ends the reading, so what follows is never checked: a control
    /// character there, or a <c>Domain</c> the host may not set, does not drop the cookie.
    /// </summary>
    [TestMethod]
    [DataRow("n=v; Pa\tth=/; X=\u0001")]
    [DataRow("n=v; Pa\tth=/x; Domain=other.test")]
    public void StoreFromResponse_RefusalAfterATabEndedPart_IsNeverReached(string header) =>
        AssertReportsOnly(CoUk, header, "Added cookie n=\"v\" for domain www.example.co.uk, path /, expire 0");

    private static void AssertReportsOnly(CurlUrl url, string header, string expectedLine)
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(url, [header], Now, events);

        CollectionAssert.AreEqual(new[] { expectedLine }, events.Info);
    }

    private static void AssertReportsNothing(string header)
    {
        RecordingTransferEvents events = new();
        CookieStore store = new();

        store.StoreFromResponse(CoUk, [header], Now, events);

        Assert.IsEmpty(events.Info);
        Assert.IsEmpty(store.Cookies);
    }
}
