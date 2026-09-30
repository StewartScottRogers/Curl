using System.Text;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>One key an ssh-agent holds, as its identities answer lists it.</summary>
/// <param name="Blob">The public key blob, which names the key type first.</param>
/// <param name="Comment">The key's comment as the agent sent it.</param>
internal sealed record SshAgentIdentity(byte[] Blob, byte[] Comment)
{
    /// <summary>
    /// Gets the comment as curl's <c>-v</c> line prints it: libssh2 keeps it as a C string,
    /// so it ends at its first zero byte; the bytes are read as UTF-8.
    /// </summary>
    internal string DisplayComment
    {
        get
        {
            int end = Array.IndexOf(Comment, (byte)0);
            return Encoding.UTF8.GetString(Comment, 0, end < 0 ? Comment.Length : end);
        }
    }
}
