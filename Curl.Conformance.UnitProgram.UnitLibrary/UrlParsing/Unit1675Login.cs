namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// What unit1675.c's <c>parse_hostname_login</c> rows read back from <c>struct Curl_URL</c>: the user, password and
/// options it holds afterwards, and the offset of the host in the text.
/// </summary>
/// <param name="User">The user, or <see langword="null"/> when there is none.</param>
/// <param name="Password">The password, or <see langword="null"/> when there is none.</param>
/// <param name="Options">The login options, or <see langword="null"/> when there are none.</param>
/// <param name="Offset">The index just after the <c>@</c>, or 0 when the text has no login.</param>
public sealed record Unit1675Login(string? User, string? Password, string? Options, int Offset);
