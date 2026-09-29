namespace Curl.Protocol.Ldap;

/// <summary>
/// The search an LDAP URL names (RFC 4516), as the reference build read it. Text is held as
/// byte strings, one <see cref="char" /> per byte, and turned into wire bytes by
/// <see cref="LdapWireText" />.
/// </summary>
/// <param name="BaseObject">The base DN; empty when the URL names none.</param>
/// <param name="Attributes">The attributes to return; empty for all of them.</param>
/// <param name="Scope">The scope: 0 <c>baseObject</c>, 1 <c>singleLevel</c>, 2 <c>wholeSubtree</c>, 3 <c>subordinateSubtree</c>.</param>
/// <param name="Filter">The filter as written, or <see langword="null" /> for the build's default <c>(objectClass=*)</c>.</param>
internal sealed record LdapSearchParameters(string BaseObject, IReadOnlyList<string> Attributes, int Scope, string? Filter);
