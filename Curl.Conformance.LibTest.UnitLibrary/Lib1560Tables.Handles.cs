using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <content><c>test_scheme_guess</c>'s and <c>clear_url</c>'s tables.</content>
public static partial class Lib1560Tables
{
    /// <summary><c>test_scheme_guess</c>'s URLs, set with and without <c>CURLU_GUESS_SCHEME</c>.</summary>
    public static IReadOnlyList<Lib1560SchemeGuessCase> SchemeGuesses { get; } =
    [
        new("https://example.com", false, CurlUrlFlags.None, "https", CurlUrlFlags.None),
        new("example.com", true, CurlUrlFlags.None, null, CurlUrlFlags.None),
        new("example.com", false, CurlUrlFlags.GuessScheme, "http", CurlUrlFlags.None),
        new("ftp.example.com:22", false, CurlUrlFlags.GuessScheme, "ftp", CurlUrlFlags.None),
        new("ftp.example.com/foo", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("dict.example.com:33", false, CurlUrlFlags.GuessScheme, "dict", CurlUrlFlags.None),
        new("dict.example.com/bar", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("ldap.example.com#none", false, CurlUrlFlags.GuessScheme, "ldap", CurlUrlFlags.None),
        new("ldap.example.com?special", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("smtp.example.com?hey#ho", false, CurlUrlFlags.GuessScheme, "smtp", CurlUrlFlags.None),
        new("smtp.example.com:99/moo", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("pop3.example.com:100#foobar", false, CurlUrlFlags.GuessScheme, "pop3", CurlUrlFlags.None),
        new("pop3.example.com:101?gg", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("example.com", false, CurlUrlFlags.GuessScheme, null, CurlUrlFlags.NoGuessScheme),
        new("https://example.com", false, CurlUrlFlags.GuessScheme, "https", CurlUrlFlags.NoGuessScheme),
        new("foobar://example.com", false, CurlUrlFlags.GuessScheme | CurlUrlFlags.NonSupportScheme, "foobar", CurlUrlFlags.NoGuessScheme),
        new("foobar://example.com", false, CurlUrlFlags.NonSupportScheme, "foobar", CurlUrlFlags.NoGuessScheme),
        new("foobar://example.com", false, CurlUrlFlags.NonSupportScheme, "foobar", CurlUrlFlags.None),
    ];

    /// <summary><c>clear_url_list</c>: each part, wanted gone once the whole URL is cleared (the path <c>/</c>).</summary>
    public static IReadOnlyList<Lib1560ClearCase> ClearUrls { get; } =
    [
        new(CurlUrlPart.Scheme, "http", null, CurlUrlCode.NoScheme),
        new(CurlUrlPart.User, "user", null, CurlUrlCode.NoUser),
        new(CurlUrlPart.Password, "password", null, CurlUrlCode.NoPassword),
        new(CurlUrlPart.Options, "options", null, CurlUrlCode.NoOptions),
        new(CurlUrlPart.Host, "host", null, CurlUrlCode.NoHost),
        new(CurlUrlPart.ZoneId, "eth0", null, CurlUrlCode.NoZoneId),
        new(CurlUrlPart.Port, "1234", null, CurlUrlCode.NoPort),
        new(CurlUrlPart.Path, "/hello", "/", CurlUrlCode.Ok),
        new(CurlUrlPart.Query, "a=b", null, CurlUrlCode.NoQuery),
        new(CurlUrlPart.Fragment, "anchor", null, CurlUrlCode.NoFragment),
    ];
}
