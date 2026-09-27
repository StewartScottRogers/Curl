namespace Curl.Conformance;

/// <summary>How sws answers a request, as <c>&lt;servercmd&gt;</c> sets its <c>rcmd</c>.</summary>
internal enum SwsReplyMode
{
    /// <summary>With the request's <c>&lt;data&gt;</c> part, as usual.</summary>
    Normal,

    /// <summary><c>idle</c>: with nothing at all, keeping the connection open.</summary>
    Idle,

    /// <summary><c>stream</c>: with <c>a string to stream 01234567890\n</c>, repeated until the client goes away.</summary>
    Stream,
}
