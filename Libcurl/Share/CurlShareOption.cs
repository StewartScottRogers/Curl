namespace Libcurl.Share;

/// <summary>
/// An option <see cref="CurlShare.SetOption"/> sets (<c>CURLSHoption</c>, <c>CURLSHOPT_*</c>), with
/// upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlShareOption
{
    /// <summary><c>CURLSHOPT_NONE</c> (0): not an option; <see cref="CurlShare.SetOption"/> answers <c>BadOption</c>.</summary>
    None = 0,

    /// <summary><c>CURLSHOPT_SHARE</c> (1): start sharing one <c>CurlLockData</c> kind.</summary>
    Share = 1,

    /// <summary><c>CURLSHOPT_UNSHARE</c> (2): stop sharing one <c>CurlLockData</c> kind.</summary>
    Unshare = 2,

    /// <summary><c>CURLSHOPT_LOCKFUNC</c> (3): the <c>ShareLockCallback</c> that takes a lock.</summary>
    LockFunction = 3,

    /// <summary><c>CURLSHOPT_UNLOCKFUNC</c> (4): the <c>ShareUnlockCallback</c> that releases a lock.</summary>
    UnlockFunction = 4,

    /// <summary><c>CURLSHOPT_USERDATA</c> (5): any value, kept as <see cref="CurlShare.UserData"/>.</summary>
    UserData = 5,
}
