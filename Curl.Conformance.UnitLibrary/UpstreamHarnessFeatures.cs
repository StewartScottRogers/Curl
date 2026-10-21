namespace Curl.Conformance;

/// <summary>
/// The features the harness reports because of what Curl and its test setup provide: <c>large_file</c>,
/// <c>local-http</c>, <c>headers-api</c> (BL-2264), <c>form-api</c> (BL-2593), <c>wakeup</c> (BL-2661),
/// <c>threadsafe</c> and <c>threaded-resolver</c> (BL-2700), and <c>codeset-utf8</c>, since Curl reads
/// URLs, mail addresses and <c>-w</c> text as UTF-8 whatever the locale, so the harness stands in for
/// the UTF-8 locale <c>runtests.pl</c> derives it from (BL-2861).
/// </summary>
internal static class UpstreamHarnessFeatures
{
    public static readonly string[] Names =
    [
        "large_file",
        "local-http",
        "headers-api",
        "form-api",
        "wakeup",
        "threadsafe",
        "threaded-resolver",
        "codeset-utf8",
    ];
}
