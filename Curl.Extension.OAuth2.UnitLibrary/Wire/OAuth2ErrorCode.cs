namespace Curl.Extension.OAuth2.Wire;

/// <summary>The <c>error</c> of an error response (RFC 6749 §5.2, RFC 8628 §3.5).</summary>
public enum OAuth2ErrorCode
{
    /// <summary>An <c>error</c> neither RFC names.</summary>
    Unrecognized,

    /// <summary><c>invalid_request</c>.</summary>
    InvalidRequest,

    /// <summary><c>invalid_client</c>.</summary>
    InvalidClient,

    /// <summary><c>invalid_grant</c>.</summary>
    InvalidGrant,

    /// <summary><c>unauthorized_client</c>.</summary>
    UnauthorizedClient,

    /// <summary><c>unsupported_grant_type</c>.</summary>
    UnsupportedGrantType,

    /// <summary><c>invalid_scope</c>.</summary>
    InvalidScope,

    /// <summary><c>authorization_pending</c>: the user has not finished yet, so poll again (RFC 8628).</summary>
    AuthorizationPending,

    /// <summary><c>slow_down</c>: poll again with the interval 5 seconds longer (RFC 8628).</summary>
    SlowDown,

    /// <summary><c>access_denied</c>: the user refused (RFC 8628).</summary>
    AccessDenied,

    /// <summary><c>expired_token</c>: the device code expired (RFC 8628).</summary>
    ExpiredToken,
}
