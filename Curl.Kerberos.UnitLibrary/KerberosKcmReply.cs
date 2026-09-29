namespace Curl.Kerberos;

/// <summary>
/// One KCM reply: the daemon's status code and what follows it. A non-zero status is the
/// daemon refusing the request, e.g. MIT's <c>KRB5_CC_NOTFOUND</c> (-1765328243).
/// </summary>
/// <param name="Status">The status code: zero for success, otherwise a Kerberos error code.</param>
/// <param name="Payload">The reply's bytes after the status code.</param>
public sealed record KerberosKcmReply(int Status, byte[] Payload)
{
    /// <summary>Gets whether the daemon answered the request with success (a zero status).</summary>
    public bool IsSuccess => Status == 0;
}
