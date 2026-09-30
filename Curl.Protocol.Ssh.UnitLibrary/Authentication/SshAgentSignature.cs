namespace Curl.Protocol.Ssh.Authentication;

/// <summary>An ssh-agent's sign response, read as libssh2 reads it.</summary>
/// <param name="Method">The signature algorithm the agent names.</param>
/// <param name="Signature">
/// The signature bytes, or <see langword="null" /> when the response ends before them:
/// libssh2 compares the method first, so a wrong method is noticed even then.
/// </param>
internal sealed record SshAgentSignature(byte[] Method, byte[]? Signature);
