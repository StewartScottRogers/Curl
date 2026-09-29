namespace Curl.Protocol.Ldap;

/// <summary>One SearchResultEntry (RFC 4511 section 4.5.2), its text as the server sent it.</summary>
/// <param name="Dn">The objectName, the entry's DN.</param>
/// <param name="Attributes">
/// The entry's attributes in the order sent, or <see langword="null" /> when they cannot be
/// read: the list is missing, or an attribute is not a type and a set of values.
/// </param>
internal sealed record LdapSearchEntry(byte[] Dn, IReadOnlyList<LdapEntryAttribute>? Attributes);
