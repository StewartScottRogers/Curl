namespace Curl.Kerberos;

/// <summary>A client principal and its password, for an AS exchange with encrypted-timestamp pre-authentication.</summary>
/// <param name="Client">The client, e.g. <c>alice@EXAMPLE.TEST</c>.</param>
/// <param name="Password">The client's password.</param>
public sealed record KerberosPasswordCredential(KerberosPrincipal Client, string Password)
{
    /// <summary>Gets <see cref="Password" /> hidden, so a logged credential never shows it.</summary>
    /// <returns>The client and a masked password.</returns>
    public override string ToString() => $"{Client} (password hidden)";
}
