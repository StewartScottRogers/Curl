namespace Curl.Kerberos;

/// <summary>A Kerberos principal name (RFC 4120 section 5.2.2) with the realm it belongs to.</summary>
/// <param name="NameType">The name type, e.g. 1 for <c>KRB_NT_PRINCIPAL</c>, 2 for <c>KRB_NT_SRV_INST</c>.</param>
/// <param name="Realm">The realm, e.g. <c>EXAMPLE.TEST</c>.</param>
/// <param name="Components">The name's components, e.g. <c>HTTP</c> and <c>server.example.test</c>.</param>
public sealed record KerberosPrincipal(int NameType, string Realm, IReadOnlyList<string> Components)
{
    /// <summary>Gets the principal as MIT's <c>klist</c> prints it, e.g. <c>HTTP/server.example.test@EXAMPLE.TEST</c>.</summary>
    /// <returns>The components joined by <c>/</c>, then <c>@</c> and the realm.</returns>
    public override string ToString() => $"{string.Join('/', Components)}@{Realm}";
}
