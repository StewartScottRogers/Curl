using Curl.Api.Codes;

namespace Curl.Api.EngineContracts.Share;

/// <summary>
/// One share handle of an <see cref="ICurlEngine"/> (ADR-0506): data that every transfer pointing
/// at it through <c>CURLOPT_SHARE</c> shares - cookies, DNS, TLS sessions, connections, the public
/// suffix list or HSTS. <c>CURLSHOPT_USERDATA</c> has no member: the callbacks' closures carry it.
/// </summary>
public interface IShareHandle
{
    /// <summary><c>curl_share_setopt</c> with <c>CURLSHOPT_SHARE</c>: start sharing one kind of data.</summary>
    /// <param name="data">The kind of data to share.</param>
    /// <returns>The call's result; <see cref="CurlShareCode.InUse"/> while a transfer uses the handle, <see cref="CurlShareCode.BadOption"/> for a kind that cannot be shared.</returns>
    CurlShareCode Share(CurlLockData data);

    /// <summary><c>curl_share_setopt</c> with <c>CURLSHOPT_UNSHARE</c>: stop sharing one kind of data.</summary>
    /// <param name="data">The kind of data to stop sharing.</param>
    /// <returns>The call's result; <see cref="CurlShareCode.InUse"/> while a transfer uses the handle.</returns>
    CurlShareCode Unshare(CurlLockData data);

    /// <summary><c>curl_share_setopt</c> with <c>CURLSHOPT_LOCKFUNC</c>: the callback that takes a lock.</summary>
    /// <param name="callback">The callback, or <see langword="null"/> for none.</param>
    /// <returns>The call's result; <see cref="CurlShareCode.InUse"/> while a transfer uses the handle.</returns>
    CurlShareCode SetLockCallback(ShareLockCallback? callback);

    /// <summary><c>curl_share_setopt</c> with <c>CURLSHOPT_UNLOCKFUNC</c>: the callback that releases a lock.</summary>
    /// <param name="callback">The callback, or <see langword="null"/> for none.</param>
    /// <returns>The call's result; <see cref="CurlShareCode.InUse"/> while a transfer uses the handle.</returns>
    CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback);

    /// <summary><c>curl_share_cleanup</c>: free the handle and what it shares.</summary>
    /// <returns>The call's result; <see cref="CurlShareCode.InUse"/>, freeing nothing, while a transfer still uses the handle.</returns>
    CurlShareCode Cleanup();
}
