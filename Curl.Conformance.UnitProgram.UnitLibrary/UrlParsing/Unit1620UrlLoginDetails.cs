using Curl.Api.Codes;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1620.c</c>, the unit tests for <c>url.c</c>. Its handle checks
/// run on Libcurl's easy handle (<see cref="CheckEasyHandleWithLibcurlAsync"/>): <c>Curl_connect</c> on a handle
/// with no URL, twice, is <see cref="CurlEasy.PerformAsync"/> answering <see cref="CurlCode.UrlMalformat"/>;
/// <c>CURLOPT_NOBODY</c> is set and <c>curl_easy_duphandle</c>'s copy must keep it (Curl has no separate
/// <c>HTTPREQ_HEAD</c>: a transfer without a body is how it asks for one); and <c>Curl_freeset</c> is
/// <see cref="CurlEasy.Reset"/>, after which no URL string may be left. The magic number and
/// <c>Curl_init_transfer</c> checks test C memory bookkeeping Curl does not have, and are not ported. Its
/// fourteen <c>t1620_parse</c> rows run on <c>Curl_parse_login_details</c>, here
/// <see cref="CurlUrlAuthority.ParseLoginDetails"/> with login options read, each checking that the user,
/// password and options are present when expected and then, while no check has failed yet, equal.
/// </summary>
/// <param name="parseLoginDetails">Splits user information into user, password and options; <see cref="ParseLoginDetailsWithCurl"/> unless a test injects another.</param>
/// <param name="checkEasyHandle">Runs the handle checks; <see cref="CheckEasyHandleWithLibcurlAsync"/> unless a test injects another.</param>
public sealed class Unit1620UrlLoginDetails(
    Func<string, (string? User, string? Password, string? Options)> parseLoginDetails,
    Func<UnitChecks, CancellationToken, Task> checkEasyHandle) : IUnitProgram
{
    private static readonly (string Input, string? User, string? Password, string? Options)[] LoginRows =
    [
        ("hostname", "hostname", null, null),
        ("user:password", "user", "password", null),
        ("user:password;options", "user", "password", "options"),
        ("user:password;options;more", "user", "password", "options;more"),
        ("", "", null, null),
        (":", "", "", null),
        (":;", "", "", null),
        (":password", "", "password", null),
        (":password;", "", "password", null),
        (";options", "", null, "options"),
        ("user;options", "user", null, "options"),
        ("user:;options", "user", "", "options"),
        ("user;options:password", "user", "password", "options"),
        ("user;options:", "user", "", "options"),
    ];

    /// <summary>Creates the port over <see cref="ParseLoginDetailsWithCurl"/> and <see cref="CheckEasyHandleWithLibcurlAsync"/>.</summary>
    public Unit1620UrlLoginDetails()
        : this(ParseLoginDetailsWithCurl, CheckEasyHandleWithLibcurlAsync)
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1620";

    /// <summary>Curl's <c>Curl_parse_login_details</c> with login options read: <see cref="CurlUrlAuthority.ParseLoginDetails"/>.</summary>
    /// <param name="login">The user information.</param>
    /// <returns>The user, password and options; the password and options are <see langword="null"/> when absent.</returns>
    public static (string? User, string? Password, string? Options) ParseLoginDetailsWithCurl(string login)
    {
        CurlUrlAuthority details = CurlUrlAuthority.ParseLoginDetails(login, hasLoginOptions: true);

        return (details.User, details.Password, details.Options);
    }

    /// <summary>
    /// The C program's handle checks on a Libcurl easy handle: performing with no URL is
    /// <see cref="CurlCode.UrlMalformat"/> twice, <c>CURLOPT_NOBODY</c> is accepted and kept by the duplicate,
    /// and a reset handle holds no URL.
    /// </summary>
    /// <param name="checks">Where each failed check is counted.</param>
    /// <param name="cancellationToken">Stops the checks early.</param>
    /// <returns>A task that completes when every check has run.</returns>
    public static async Task CheckEasyHandleWithLibcurlAsync(UnitChecks checks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checks);
        await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.All);
        await using CurlEasy empty = library.EasyInit();
        CurlCode result = await empty.PerformAsync(cancellationToken).ConfigureAwait(false);
        checks.FailUnless(result == CurlCode.UrlMalformat, "Curl_connect() failed to return CURLE_URL_MALFORMAT");
        result = await empty.PerformAsync(cancellationToken).ConfigureAwait(false);
        checks.FailUnless(result == CurlCode.UrlMalformat, "Curl_connect() failed to return CURLE_URL_MALFORMAT");
        checks.FailUnless(empty.SetOption(CurlOption.Nobody, 1L) == CurlCode.Ok, "curl_easy_setopt(CURLOPT_NOBODY) failed");
        await using CurlEasy dupe = empty.DupHandle();
        checks.FailUnless(dupe.Options.Transfer.NoBody, "duplicate handle should keep no_body");
        empty.Reset();
        checks.FailUnless(empty.Options.Network.Url is null, "Curl_free() did not set to NULL");
    }

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        await checkEasyHandle(checks, cancellationToken).ConfigureAwait(false);
        foreach ((string input, string? user, string? password, string? options) in LoginRows)
        {
            CheckLoginRow(checks, parseLoginDetails(input), (user, password, options));
        }

        return checks.Result;
    }

    private static void CheckLoginRow(
        UnitChecks checks,
        (string? User, string? Password, string? Options) actual,
        (string? User, string? Password, string? Options) expected)
    {
        checks.FailUnless(actual.User is null == expected.User is null, "username expectation failed");
        checks.FailUnless(actual.Password is null == expected.Password is null, "password expectation failed");
        checks.FailUnless(actual.Options is null == expected.Options is null, "options expectation failed");
        if (checks.Result.FailureCount != 0)
        {
            return;
        }

        checks.FailUnless(actual.User == expected.User, "userstr should be equal to exp_username");
        checks.FailUnless(actual.Password == expected.Password, "passwdstr should be equal to exp_password");
        checks.FailUnless(actual.Options == expected.Options, "options should be equal to exp_options");
    }
}
