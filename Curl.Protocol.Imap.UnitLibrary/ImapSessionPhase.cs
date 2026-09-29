namespace Curl.Protocol.Imap;

/// <summary>
/// How far an <see cref="ImapSession" /> got, which decides the line curl 8.21.0's <c>-v</c>
/// ends a failed transfer with (measured, BL-559).
/// </summary>
internal enum ImapSessionPhase
{
    /// <summary>Not yet logged in: a failure ends with <c>closing connection</c>.</summary>
    Opening,

    /// <summary>Logged in, no literal moving yet: a failure ends with <c>shutting down connection</c>.</summary>
    Performing,

    /// <summary>A literal is being read: a failure ends with <c>closing connection</c>.</summary>
    Transferring,

    /// <summary>
    /// The literal is through and the command's completion is read: a failure ends with
    /// <c>left intact</c>, as a success does.
    /// </summary>
    Completing,
}
