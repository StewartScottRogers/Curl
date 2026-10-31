namespace Curl.Original.Protocol.Ftp;

/// <summary>What one <c>[...]</c> bracket expression of a system <c>fnmatch</c> pattern answers for one character.</summary>
internal enum FtpBracketOutcome
{
    /// <summary>The character is in the bracket expression (negation applied).</summary>
    Matched,

    /// <summary>The character is not in it: the pattern does not match at this position.</summary>
    NotMatched,

    /// <summary>glibc: the bracket is never closed, so its <c>[</c> is an ordinary character.</summary>
    Unterminated,

    /// <summary>macOS: the bracket is malformed, so <c>fnmatch</c> fails as a whole.</summary>
    Failed,
}
