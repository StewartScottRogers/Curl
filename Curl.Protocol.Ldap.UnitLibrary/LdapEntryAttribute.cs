namespace Curl.Protocol.Ldap;

/// <summary>One PartialAttribute of a SearchResultEntry (RFC 4511 section 4.1.7), as the server sent it.</summary>
/// <param name="Name">The attribute description, such as <c>cn</c> or <c>userCertificate;binary</c>.</param>
/// <param name="Values">Its values in the order sent; empty when the server sent none.</param>
internal sealed record LdapEntryAttribute(byte[] Name, IReadOnlyList<byte[]> Values);
