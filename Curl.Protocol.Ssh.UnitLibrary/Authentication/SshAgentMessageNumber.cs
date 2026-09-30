namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The ssh-agent message numbers and sign flags (draft-miller-ssh-agent sections 3 and 4)
/// that <see cref="SshAgentClient" /> writes or reads.
/// </summary>
internal static class SshAgentMessageNumber
{
    /// <summary><c>SSH_AGENTC_REQUEST_IDENTITIES</c>: asks for the keys the agent holds.</summary>
    internal const byte RequestIdentities = 11;

    /// <summary><c>SSH_AGENT_IDENTITIES_ANSWER</c>: the keys, each a public key blob and a comment.</summary>
    internal const byte IdentitiesAnswer = 12;

    /// <summary><c>SSH_AGENTC_SIGN_REQUEST</c>: asks for a signature by one key over some data.</summary>
    internal const byte SignRequest = 13;

    /// <summary><c>SSH_AGENT_SIGN_RESPONSE</c>: the signature.</summary>
    internal const byte SignResponse = 14;

    /// <summary><c>SSH_AGENT_RSA_SHA2_256</c>: sign an RSA key's data with <c>rsa-sha2-256</c>.</summary>
    internal const uint RsaSha2256Flag = 2;

    /// <summary><c>SSH_AGENT_RSA_SHA2_512</c>: sign an RSA key's data with <c>rsa-sha2-512</c>.</summary>
    internal const uint RsaSha2512Flag = 4;
}
