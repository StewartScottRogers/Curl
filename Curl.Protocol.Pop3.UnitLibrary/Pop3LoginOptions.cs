namespace Curl.Protocol.Pop3;

/// <summary>
/// Which way a POP3 session may log in, read from the login options (<c>--login-options</c>,
/// else the URL's <c>;</c> options) as curl 8.21.0's <c>pop3_parse_url_options</c> reads them.
/// </summary>
/// <param name="Method">The ways allowed.</param>
/// <param name="RequiredMechanism">
/// The SASL mechanism <c>AUTH=&lt;mech&gt;</c> named, in the case it was written, or
/// <see langword="null" /> when the options named none.
/// </param>
/// <remarks>
/// Measured with <c>Record-CurlExchange.ps1 -Pop3</c> (BL-548): keys and values are compared
/// case-insensitively (<c>auth=+apop</c> works); <c>AUTH=*</c> is the same as no option;
/// <c>AUTH=+APOP</c> allows only <c>APOP</c>; <c>AUTH=&lt;mech&gt;</c> allows only that SASL
/// mechanism; a mechanism curl does not know (<c>AUTH=USER</c>) or any other key
/// (<c>FOO=bar</c>) is exit 3 before a byte is sent. Of several <c>AUTH=</c> options the
/// last one counts.
/// </remarks>
internal sealed record Pop3LoginOptions(Pop3LoginMethod Method, string? RequiredMechanism)
{
    private const string AuthKey = "AUTH=";

    private const string AnyValue = "*";

    private const string ApopValue = "+APOP";

    /// <summary>The login options that allow every way, as no option does.</summary>
    public static readonly Pop3LoginOptions Any = new(Pop3LoginMethod.Any, null);

    /// <summary>
    /// The SASL mechanism names curl 8.21.0 knows, which <c>AUTH=</c> may name.
    /// </summary>
    private static readonly string[] KnownMechanisms =
    [
        "LOGIN", "PLAIN", "CRAM-MD5", "DIGEST-MD5", "GSSAPI", "EXTERNAL", "NTLM", "XOAUTH2", "OAUTHBEARER",
        "SCRAM-SHA-1", "SCRAM-SHA-256",
    ];

    /// <summary>
    /// Tells whether <paramref name="mechanism" /> is a SASL mechanism curl 8.21.0 knows, in
    /// any case (<c>SASL plain</c> measured as known in BL-810).
    /// </summary>
    /// <param name="mechanism">A mechanism name, such as one <c>CAPA</c> listed.</param>
    /// <returns><see langword="true" /> when curl knows it.</returns>
    public static bool IsKnownMechanism(string mechanism) =>
        KnownMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads <paramref name="options" />.
    /// </summary>
    /// <param name="options">The login options, such as <c>AUTH=PLAIN</c>, or <see langword="null" /> for none.</param>
    /// <returns>What they allow, or <see langword="null" /> when curl refuses them with exit 3.</returns>
    public static Pop3LoginOptions? Read(string? options)
    {
        Pop3LoginOptions result = Any;
        foreach (string option in (options ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!option.StartsWith(AuthKey, StringComparison.OrdinalIgnoreCase)
                || ReadAuthValue(option[AuthKey.Length..]) is not { } read)
            {
                return null;
            }

            result = read;
        }

        return result;
    }

    private static Pop3LoginOptions? ReadAuthValue(string value)
    {
        if (value == AnyValue)
        {
            return Any;
        }

        if (value.Equals(ApopValue, StringComparison.OrdinalIgnoreCase))
        {
            return new Pop3LoginOptions(Pop3LoginMethod.Apop, null);
        }

        return IsKnownMechanism(value)
            ? new Pop3LoginOptions(Pop3LoginMethod.Sasl, value)
            : null;
    }
}
