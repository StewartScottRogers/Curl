using Curl.Protocol.Ssh.HostKeys;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Reads a <c>--pubkey</c> file as libssh2 1.11.1's <c>file_read_publickey</c> does: the
/// OpenSSH one-line form <c>&lt;type&gt; &lt;base64&gt; [comment]</c>, first line only.
/// </summary>
internal static class SshPublicKeyFile
{
    /// <summary>
    /// Parses the file's text. The key type is everything before the first space, taken as
    /// written; the blob is the base64 between the first and second space (or the line's
    /// end), decoded leniently as libssh2 decodes it.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>
    /// The public key, or <see langword="null" /> when the first line is one character or
    /// less, blank, has no space, or holds no valid base64: libssh2 then fails the method
    /// without sending anything.
    /// </returns>
    internal static SshPublicKey? Parse(string text)
    {
        int lineEnd = text.AsSpan().IndexOfAny('\r', '\n');
        string line = lineEnd < 0 ? text : text[..lineEnd];
        string trimmed = line.TrimEnd();
        int firstSpace = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return line.Length <= 1 || firstSpace < 0 ? null : Decode(trimmed[..firstSpace], trimmed[(firstSpace + 1)..]);
    }

    private static SshPublicKey? Decode(string keyType, string rest)
    {
        int secondSpace = rest.IndexOf(' ', StringComparison.Ordinal);
        string data = secondSpace < 0 ? rest : rest[..secondSpace];
        return Libssh2Base64.TryDecode(data, out byte[]? blob) && blob!.Length > 0 ? new SshPublicKey(keyType, blob) : null;
    }
}
