namespace Curl.Protocol.Abstractions;

/// <summary>
/// How much of Curl's own diagnostic log <c>--log-level</c> asks for (ADR-0222). Levels
/// are cumulative: each one includes every level below it.
/// </summary>
public enum DiagnosticLogLevel
{
    /// <summary>
    /// No diagnostic log at all, the default: zero extra bytes anywhere.
    /// </summary>
    None = 0,

    /// <summary>
    /// The failure that ends a transfer, with its exit code and underlying exception.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Something recovered from: a retry, a fallback, input ignored.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Milestones: resolved, connected, TLS negotiated, request sent, reply status, done.
    /// </summary>
    Info = 3,

    /// <summary>
    /// Every decision and state-machine step.
    /// </summary>
    Verbose = 4,
}
