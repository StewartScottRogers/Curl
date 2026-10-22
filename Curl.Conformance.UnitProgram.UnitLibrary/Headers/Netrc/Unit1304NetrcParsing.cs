using Curl.Authentication;
using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram.Headers.Netrc;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1304.c</c>: <c>Curl_netrc_scan</c>, here
/// <see cref="NetrcFile.Find" />, eight times over the netrc file whose path is the program's one argument -
/// a host, a login and both unknown, a login that is a substring and one that is a superstring of
/// <c>admin</c>, <c>example.com</c> and <c>curl.example.com</c> with no login, and <c>curl.example.com</c>
/// with the login <c>hilarious</c>, check for check with the C program.
/// </summary>
public sealed class Unit1304NetrcParsing : IUnitProgram
{
    /// <inheritdoc />
    public string ProgramName => "unit1304";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string text = await File.ReadAllTextAsync(context.Arguments[0], cancellationToken);
        UnitChecks checks = new(context.StandardError, ProgramName);

        // A non existent host in our netrc file.
        NetrcLookupResult unknownHost = NetrcFile.Find(text, "test.example.com", null);
        checks.FailUnless(unknownHost.Outcome == NetrcLookupOutcome.NotFound, "expected no match");
        checks.FailUnless(unknownHost.Login is null && unknownHost.Password is null, "creds did not return NULL!");

        // A non existent login, a non existent login and host, and logins that are a substring and a superstring of an existing one.
        CheckNoMatch(checks, NetrcFile.Find(text, "example.com", "me"), "password is not NULL!");
        CheckNoMatch(checks, NetrcFile.Find(text, "test.example.com", "me"), "password is not NULL!");
        CheckNoMatch(checks, NetrcFile.Find(text, "example.com", "a"), "password is not NULL!");
        CheckNoMatch(checks, NetrcFile.Find(text, "example.com", "administrator"), "password is not NULL!");

        // The first and the second existing host with no user.
        CheckFound(checks, NetrcFile.Find(text, "example.com", null), "admin", "passwd");
        CheckFound(checks, NetrcFile.Find(text, "curl.example.com", null), "none", "none");

        // The last host, whose password is not wanted when the login does not match.
        NetrcLookupResult hilarious = NetrcFile.Find(text, "curl.example.com", "hilarious");
        checks.FailUnless(hilarious.Outcome == NetrcLookupOutcome.NotFound, "expect no match");
        checks.FailUnless(string.IsNullOrEmpty(hilarious.Password), "password must be NULL");
        return checks.Result;
    }

    private static void CheckNoMatch(UnitChecks checks, NetrcLookupResult result, string passwordMessage)
    {
        checks.FailUnless(result.Outcome == NetrcLookupOutcome.NotFound, "expected no match");
        checks.FailUnless(string.IsNullOrEmpty(result.Password), passwordMessage);
    }

    private static void CheckFound(UnitChecks checks, NetrcLookupResult result, string login, string password)
    {
        // Curl_creds_user and Curl_creds_passwd give an empty string for creds that have none.
        checks.FailUnless(result.Outcome == NetrcLookupOutcome.Found, "Host should have been found");
        checks.FailUnless((result.Password ?? string.Empty).StartsWith(password, StringComparison.Ordinal), $"password should be '{password}'");
        checks.FailUnless(!string.IsNullOrEmpty(result.Login), "returned NULL!");
        checks.FailUnless((result.Login ?? string.Empty).StartsWith(login, StringComparison.Ordinal), $"login should be '{login}'");
    }
}
