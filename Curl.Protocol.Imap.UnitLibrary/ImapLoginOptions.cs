namespace Curl.Protocol.Imap;

/// <summary>
/// How the login options (<c>--login-options</c>, or the URL's <c>;</c> options when it is
/// not given) let an IMAP session authenticate, read as curl 8.21.0's
/// <c>imap_parse_url_options</c> reads them and measured with
/// <c>Record-CurlExchange.ps1 -Imap</c> (BL-554).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The options are <c>;</c>-separated and each must start <c>AUTH=</c>, in any case;
/// one ending <c>;</c> is allowed. Any other option, an empty <c>AUTH=</c> or a mechanism
/// curl does not know is exit 3 before anything is sent.</item>
/// <item>With no <c>AUTH=</c> option any SASL mechanism but <c>EXTERNAL</c> may be used, and
/// <c>LOGIN</c> after it. The first <c>AUTH=&lt;mech&gt;</c> or <c>AUTH=*</c> clears that; each
/// <c>AUTH=&lt;mech&gt;</c> (in any case) then allows that mechanism, and <c>AUTH=*</c> any
/// but <c>EXTERNAL</c> again, dropping the ones named before it.</item>
/// <item><c>AUTH=+LOGIN</c>, in any case, allows no SASL mechanism, only the <c>LOGIN</c>
/// command. <c>LOGIN</c> is allowed when the last option is <c>AUTH=+LOGIN</c> or
/// <c>AUTH=*</c> allows any mechanism.</item>
/// </list>
/// </remarks>
internal sealed class ImapLoginOptions
{
    private const string AuthOption = "AUTH=";

    private const string PreferLoginOption = "AUTH=+LOGIN";

    private const string AnyMechanism = "*";

    /// <summary>The SASL mechanisms curl 8.21.0 knows by name (<c>lib/curl_sasl.c</c>).</summary>
    private static readonly HashSet<string> KnownMechanisms = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOGIN", "PLAIN", "CRAM-MD5", "DIGEST-MD5", "GSSAPI", "EXTERNAL", "NTLM", "XOAUTH2", "OAUTHBEARER", "SCRAM-SHA-1", "SCRAM-SHA-256",
    };

    private readonly HashSet<string> namedMechanisms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether an <c>AUTH=&lt;mech&gt;</c> or <c>AUTH=*</c> option has been read yet.</summary>
    private bool mechanismRead;

    private ImapLoginOptions()
    {
    }

    /// <summary>
    /// Gets a value indicating whether any SASL mechanism but <c>EXTERNAL</c> may be used,
    /// besides those <see cref="NamedMechanisms" /> names.
    /// </summary>
    public bool AllowsAnyMechanism { get; private set; } = true;

    /// <summary>Gets the SASL mechanisms the options name one by one, compared in any case.</summary>
    public IReadOnlySet<string> NamedMechanisms => namedMechanisms;

    /// <summary>Gets a value indicating whether the last option was <c>AUTH=+LOGIN</c>.</summary>
    public bool PrefersLogin { get; private set; }

    /// <summary>Gets a value indicating whether the <c>LOGIN</c> command may be sent.</summary>
    public bool AllowsLogin => PrefersLogin || AllowsAnyMechanism;

    /// <summary>
    /// Gets a value indicating whether an <c>AUTH=</c> option was given, replacing the
    /// mechanisms curl prefers by default (BL-1219).
    /// </summary>
    public bool NamesMechanisms { get; private set; }

    /// <summary>
    /// Tells whether the options leave <paramref name="mechanism" /> among curl's preferred
    /// mechanisms: one they name, or any but <c>EXTERNAL</c> while they allow any (BL-1219).
    /// </summary>
    /// <param name="mechanism">A mechanism curl knows, in any case.</param>
    /// <returns><see langword="true" /> when curl may choose it.</returns>
    public bool Prefers(string mechanism) =>
        AllowsAnyMechanism ? !string.Equals(mechanism, "EXTERNAL", StringComparison.OrdinalIgnoreCase) : namedMechanisms.Contains(mechanism);

    /// <summary>
    /// Tells whether curl 8.21.0 knows <paramref name="mechanism" /> by name, in any case.
    /// </summary>
    /// <param name="mechanism">A mechanism the server advertised.</param>
    /// <returns><see langword="true" /> when curl knows it.</returns>
    public static bool IsKnownMechanism(string mechanism) => KnownMechanisms.Contains(mechanism);

    /// <summary>
    /// Reads <paramref name="options" />.
    /// </summary>
    /// <param name="options">The login options, or <see langword="null" /> when there are none.</param>
    /// <returns>What they allow, or <see langword="null" /> when curl rejects them with exit 3.</returns>
    public static ImapLoginOptions? Parse(string? options)
    {
        var parsed = new ImapLoginOptions();
        string[] segments = (options ?? string.Empty).Split(';');
        int count = segments[^1].Length == 0 ? segments.Length - 1 : segments.Length;
        parsed.NamesMechanisms = count > 0;
        return segments.Take(count).All(parsed.TryAdd) ? parsed : null;
    }

    /// <summary>
    /// Narrows <paramref name="offered" /> to the mechanisms these options allow.
    /// </summary>
    /// <param name="offered">The mechanisms the server advertised.</param>
    /// <returns>Those of them that may be chosen.</returns>
    public List<string> AllowedAmong(IReadOnlyList<string> offered) =>
        [.. offered.Where(mechanism => AllowsAnyMechanism || namedMechanisms.Contains(mechanism))];

    /// <summary>
    /// The mechanism an <c>AUTH=</c> option names, <c>*</c> included, or
    /// <see langword="null" /> when the option is not <c>AUTH=</c> or names none curl knows.
    /// </summary>
    private static string? MechanismOf(string option)
    {
        string mechanism = option.StartsWith(AuthOption, StringComparison.OrdinalIgnoreCase) ? option[AuthOption.Length..] : string.Empty;
        return mechanism == AnyMechanism || KnownMechanisms.Contains(mechanism) ? mechanism : null;
    }

    /// <summary>Reads one option; <see langword="false" /> when curl rejects it.</summary>
    private bool TryAdd(string option)
    {
        if (option.StartsWith(PreferLoginOption, StringComparison.OrdinalIgnoreCase))
        {
            PrefersLogin = true;
            AllowsAnyMechanism = false;
            namedMechanisms.Clear();
            return true;
        }

        if (MechanismOf(option) is not { } mechanism)
        {
            return false;
        }

        AddMechanism(mechanism);
        return true;
    }

    /// <summary>
    /// Allows <paramref name="mechanism" />, or any mechanism for <c>*</c>; the first one read
    /// replaces the "any" that holds while no <c>AUTH=</c> option is given.
    /// </summary>
    private void AddMechanism(string mechanism)
    {
        bool isAny = mechanism == AnyMechanism;
        PrefersLogin = false;
        if (!mechanismRead || isAny)
        {
            namedMechanisms.Clear();
            AllowsAnyMechanism = isAny;
            mechanismRead = true;
        }

        if (!isAny)
        {
            namedMechanisms.Add(mechanism);
        }
    }
}
