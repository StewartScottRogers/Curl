namespace Curl.Console;

// Self-test fixture for Measure-ExitCodeGap.ps1: FailedInit differs by one character,
// CouldntResolveProxy has no entry.
internal static class CurlEasyErrorText
{
    public const string UnknownError = "Unknown error";

    private static readonly Dictionary<CurlExitCode, string> Texts = new()
    {
        [CurlExitCode.Ok] = "No error",
        [CurlExitCode.UnsupportedProtocol] = "Unsupported protocol",
        [CurlExitCode.FailedInit] = "Failed initialisation",
        [CurlExitCode.UrlMalformat] = "Quote \"x\" and \\ here",
        [CurlExitCode.NotBuiltIn] = "A feature was not built-in in this libcurl.",
    };
}
