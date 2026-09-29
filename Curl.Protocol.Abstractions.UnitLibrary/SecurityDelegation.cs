namespace Curl.Protocol.Abstractions;

/// <summary>Whether the initiator lets the acceptor act on its behalf: curl's <c>--delegation</c> levels.</summary>
public enum SecurityDelegation
{
    /// <summary><c>none</c>: never, curl's default.</summary>
    None,

    /// <summary><c>policy</c>: only when the service's ticket says it is trusted for delegation.</summary>
    Policy,

    /// <summary><c>always</c>: unconditionally.</summary>
    Always,
}
