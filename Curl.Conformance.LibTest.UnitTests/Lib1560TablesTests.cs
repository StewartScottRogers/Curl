using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1560TablesTests
{
    private const CurlUrlFlags None = CurlUrlFlags.None;
    private const CurlUrlFlags Guess = CurlUrlFlags.GuessScheme;
    private const CurlUrlFlags NoGuess = CurlUrlFlags.NoGuessScheme;
    private const CurlUrlFlags NonSupport = CurlUrlFlags.NonSupportScheme;

    [TestMethod]
    public void SchemeGuesses_EveryRow_MatchesUpstreamLib1560GuessTable()
    {
        Lib1560SchemeGuessCase[] upstream =
        [
            new("https://example.com", false, None, "https", None),
            new("example.com", true, None, null, None),
            new("example.com", false, Guess, "http", None),
            new("ftp.example.com:22", false, Guess, "ftp", None),
            new("ftp.example.com/foo", false, Guess, null, NoGuess),
            new("dict.example.com:33", false, Guess, "dict", None),
            new("dict.example.com/bar", false, Guess, null, NoGuess),
            new("ldap.example.com#none", false, Guess, "ldap", None),
            new("ldap.example.com?special", false, Guess, null, NoGuess),
            new("smtp.example.com?hey#ho", false, Guess, "smtp", None),
            new("smtp.example.com:99/moo", false, Guess, null, NoGuess),
            new("pop3.example.com:100#foobar", false, Guess, "pop3", None),
            new("pop3.example.com:101?gg", false, Guess, null, NoGuess),
            new("example.com", false, Guess, null, NoGuess),
            new("https://example.com", false, Guess, "https", NoGuess),
            new("foobar://example.com", false, Guess | NonSupport, "foobar", NoGuess),
            new("foobar://example.com", false, NonSupport, "foobar", NoGuess),
            new("foobar://example.com", false, NonSupport, "foobar", None),
        ];

        CollectionAssert.AreEqual(upstream, Lib1560Tables.SchemeGuesses.ToArray());
    }
}
