namespace Curl.Authentication;

/// <summary>A NegTokenResp's <c>negState</c> (RFC 4178 section 4.2.2).</summary>
internal enum SpnegoNegotiationState
{
    /// <summary><c>accept-completed</c>: the acceptor has finished; no more tokens are needed.</summary>
    AcceptCompleted = 0,

    /// <summary><c>accept-incomplete</c>: the acceptor wants another token.</summary>
    AcceptIncomplete = 1,

    /// <summary><c>reject</c>: the acceptor refused the context.</summary>
    Reject = 2,

    /// <summary><c>request-mic</c>: the acceptor wants the initiator's <c>mechListMIC</c>.</summary>
    RequestMic = 3,
}
