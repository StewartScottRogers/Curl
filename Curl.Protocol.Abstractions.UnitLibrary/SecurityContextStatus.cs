namespace Curl.Protocol.Abstractions;

/// <summary>What one <see cref="ISecurityContext.NextTokenAsync" /> step came to (ADR-0142).</summary>
public enum SecurityContextStatus
{
    /// <summary>A token was made and the peer's answer is needed to go on.</summary>
    ContinueNeeded,

    /// <summary>The context is established; the token, possibly empty, is the last to send.</summary>
    Completed,

    /// <summary>No credential is at hand: no ticket in the cache, no logged-on user's credential, or one that is expired or unknown.</summary>
    NoCredentials,

    /// <summary>No implementation of the mechanism is available here.</summary>
    NoMechanism,

    /// <summary>The peer, a KDC or the mechanism refused the exchange.</summary>
    Refused,

    /// <summary>The peer's token cannot be read.</summary>
    MalformedToken,
}
