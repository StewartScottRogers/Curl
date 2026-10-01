using Curl.Protocol.Ssh.HostKeys;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Reads a <c>--pubkey</c> file as libssh2 1.11.1's <c>file_read_publickey</c> does: the
/// OpenSSH one-line form <c>&lt;type&gt; &lt;base64&gt; [comment]</c>, first line only.
/// </summary>
internal static class SshPublicKeyFile
{
    // The characters C's isspace counts, less the line ends that already ended the line.
    private static readonly char[] TrailingWhiteSpace = [' ', '\t', '\v', '\f'];

    /// <summary>
    /// Parses the file's text. The key type is everything before the first space, taken as
    /// written; the blob is the base64 between the first and second space (or the line's
    /// end), decoded leniently as libssh2 decodes it, and may be empty.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>
    /// The public key, or libssh2's reason when there is none - the first line one
    /// character or less, blank, without a space, or holding invalid base64 - and libssh2
    /// fails the method without sending anything.
    /// </returns>
    internal static SshPublicKeyReading Parse(string text)
    {
        int lineEnd = text.AsSpan().IndexOfAny('\r', '\n');
        string line = lineEnd < 0 ? text : text[..lineEnd];
        string trimmed = line.TrimEnd(TrailingWhiteSpace);
        int firstSpace = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (line.Length <= 1)
        {
            return Denied(SshInfoLines.PublicKeyFileLineTooShort);
        }

        if (trimmed.Length == 0)
        {
            return Denied(SshInfoLines.PublicKeyFileBlank);
        }

        return firstSpace < 0 ? Denied(SshInfoLines.PublicKeyFileWithoutSpace) : Decode(trimmed[..firstSpace], trimmed[(firstSpace + 1)..]);
    }

    private static SshPublicKeyReading Decode(string keyType, string rest)
    {
        int secondSpace = rest.IndexOf(' ', StringComparison.Ordinal);
        string data = secondSpace < 0 ? rest : rest[..secondSpace];
        return Libssh2Base64.TryDecode(data, out byte[]? blob)
            ? new SshPublicKeyReading(new SshPublicKey(keyType, blob!), null)
            : Denied(SshInfoLines.PublicKeyFileNotBase64);
    }

    private static SshPublicKeyReading Denied(string reason) => new(null, reason);
}
