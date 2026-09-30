namespace Curl.Protocol.Ssh.Compression;

/// <summary>
/// The compression methods this build implements beside <c>none</c> (ADR-0122), and when
/// each one starts compressing a direction's packets.
/// </summary>
internal static class SshCompressionMethods
{
    /// <summary>
    /// RFC 4253's <c>zlib</c>: one zlib stream per direction for the session, starting
    /// with the first packet after <c>NEWKEYS</c>.
    /// </summary>
    internal const string Zlib = "zlib";

    /// <summary>
    /// OpenSSH's delayed <c>zlib@openssh.com</c>: as <see cref="Zlib" />, but starting with
    /// the first packet after <c>SSH_MSG_USERAUTH_SUCCESS</c>.
    /// </summary>
    internal const string DelayedZlib = "zlib@openssh.com";

    /// <summary>Gets the names, for the catalogue of implemented algorithms.</summary>
    internal static IReadOnlyList<string> Names { get; } = [Zlib, DelayedZlib];
}
