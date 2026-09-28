namespace Curl.Protocol.Imap;

/// <summary>
/// How a tagged response (or the greeting, which curl reads as a response tagged <c>*</c>)
/// completed, judged as curl 8.21.0 judges it: by the case-sensitive word after the tag.
/// </summary>
internal enum ImapResponseStatus
{
    /// <summary>The word after the tag starts with <c>OK</c>.</summary>
    Ok,

    /// <summary>The word after the tag starts with <c>PREAUTH</c>.</summary>
    Preauth,

    /// <summary>Anything else: <c>NO</c>, <c>BAD</c>, <c>BYE</c>, <c>ok</c> or a word curl does not know.</summary>
    NotOk,
}
