namespace Curl.Protocol.Ssh;

/// <summary>
/// A read from or a write to the SSH connection failed - the server reset it, or the
/// socket failed otherwise - as opposed to an <see cref="IOException" /> from the local
/// output or upload source, which a transfer reports on its own (BL-1046).
/// </summary>
/// <param name="innerException">The connection's own failure.</param>
internal sealed class SshConnectionLostException(IOException innerException)
    : IOException("The SSH connection failed: " + innerException.Message, innerException);
