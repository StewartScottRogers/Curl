namespace Curl.Conformance.Differential;

/// <summary>
/// A known limitation of the reference curl 8.22.0 build (Git for Windows' ucrt64 debug build) that
/// makes a case's difference the reference's fault rather than Curl's (ADR-0782). A case tagged with
/// one is not run against that build.
/// </summary>
public enum ReferenceBuildLimitation
{
    /// <summary>The reference reads its command line as ANSI, so non-ASCII arguments arrive mangled.</summary>
    AnsiArguments,

    /// <summary>The reference has a 32-bit <c>time_t</c>, so dates past 2038 overflow.</summary>
    ThirtyTwoBitTimeT,

    /// <summary>The reference is built without HTTP/2.</summary>
    NoHttp2,

    /// <summary>The reference's IDN conversion produces wrong punycode for non-ASCII host names.</summary>
    BrokenIdn,

    /// <summary>The reference is built without <c>--dns-servers</c> (no c-ares), so the option is unsupported.</summary>
    NoDnsServers,

    /// <summary>The reference is built without ECH, so <c>--ech</c> is unsupported.</summary>
    NoEch,

    /// <summary>The reference is a debug build, which alone has <c>--test-event</c>; the platform's curl has not.</summary>
    DebugOnlyTestEvent,

    /// <summary>The reference prompts for a missing password on the console, not on standard input, so it waits until the run times out.</summary>
    PasswordPromptFromConsole,

    /// <summary>The reference is built with libpsl, which adds <c>PSL check set-cookie</c> verbose lines the platform's curl does not write.</summary>
    PublicSuffixListVerboseLines,

    /// <summary>The reference lists more features in <c>--version</c> (zlib, brotli, libidn2 and others) than the platform's curl.</summary>
    DifferentFeatureList,

    /// <summary>The platform's Schannel, not only the reference build, refuses every P12 client credential with <c>SEC_E_UNKNOWN_CREDENTIALS</c> (exit 35), so a P12 client certificate cannot be compared (ADR-0794).</summary>
    SchannelRefusesP12Credential,
}
