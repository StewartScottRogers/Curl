namespace Curl.Protocol.Imap;

/// <summary>
/// Writes a mailbox name as an argument of an IMAP command, as curl 8.21.0's
/// <c>imap_atom</c> does and as measured with <c>Record-CurlExchange.ps1 -Imap</c> (BL-555).
/// </summary>
internal static class ImapQuoting
{
    /// <summary>The characters that make curl quote a name.</summary>
    private static readonly System.Buffers.SearchValues<char> Specials = System.Buffers.SearchValues.Create("() {%*]\\\"");

    /// <summary>
    /// <paramref name="name" /> as it is when it holds none of <c>( ) { % * ] \ "</c> or a
    /// space; otherwise in double quotes, with each <c>\</c> and <c>"</c> escaped by a
    /// <c>\</c>, so <c>My Box</c> is sent <c>"My Box"</c>.
    /// </summary>
    /// <param name="name">The decoded mailbox name.</param>
    /// <returns>The name as the command carries it.</returns>
    public static string AtomOrQuoted(string name) =>
        name.AsSpan().IndexOfAny(Specials) < 0
            ? name
            : "\"" + name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
