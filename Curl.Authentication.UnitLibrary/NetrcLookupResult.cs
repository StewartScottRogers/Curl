using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// What <see cref="NetrcFile" /> found for a host: the entry's login and password, no entry,
/// or a syntax error the caller reports as curl does.
/// </summary>
public sealed class NetrcLookupResult
{
    /// <summary>
    /// The message curl 8.21.0 prints after <c>curl: (26) </c> for a malformed netrc file.
    /// </summary>
    public const string SyntaxErrorMessage = ".netrc error: syntax error";

    private NetrcLookupResult(NetrcLookupOutcome outcome, string? login, string? password)
    {
        Outcome = outcome;
        Login = login;
        Password = password;
    }

    /// <summary>Gets the result of a lookup that matched no entry.</summary>
    public static NetrcLookupResult NotFound { get; } = new(NetrcLookupOutcome.NotFound, login: null, password: null);

    /// <summary>Gets the result of a lookup in a malformed file.</summary>
    public static NetrcLookupResult SyntaxError { get; } = new(NetrcLookupOutcome.SyntaxError, login: null, password: null);

    /// <summary>Gets how the lookup ended.</summary>
    public NetrcLookupOutcome Outcome { get; }

    /// <summary>
    /// Gets the matched entry's <c>login</c>; <see langword="null" /> when the entry has none,
    /// in which case curl uses the URL's user name, or an empty one.
    /// </summary>
    public string? Login { get; }

    /// <summary>
    /// Gets the matched entry's <c>password</c>; <see langword="null" /> when the entry has
    /// none, in which case curl sends an empty password.
    /// </summary>
    public string? Password { get; }

    /// <summary>
    /// Gets the exit code curl ends with for this result: <see cref="CurlExitCode.ReadError" />
    /// for a syntax error, <see cref="CurlExitCode.Ok" /> otherwise, since a lookup that
    /// finds nothing does not fail the transfer.
    /// </summary>
    public CurlExitCode ExitCode => Outcome == NetrcLookupOutcome.SyntaxError ? CurlExitCode.ReadError : CurlExitCode.Ok;

    /// <summary>Creates the result of a lookup that matched an entry.</summary>
    /// <param name="login">The entry's <c>login</c>; <see langword="null" /> when it has none.</param>
    /// <param name="password">The entry's <c>password</c>; <see langword="null" /> when it has none.</param>
    /// <returns>A <see cref="NetrcLookupOutcome.Found" /> result.</returns>
    internal static NetrcLookupResult Found(string? login, string? password) =>
        new(NetrcLookupOutcome.Found, login, password);
}
