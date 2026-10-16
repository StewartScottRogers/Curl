namespace Libcurl.Global;

/// <summary>
/// The <c>CURL_GLOBAL_*</c> flags <c>curl_global_init</c> takes, with upstream curl 8.22.0's numbers
/// from <c>include/curl/curl.h</c>. The managed engine needs none of the work they ask for - .NET
/// sockets need no <c>WSAStartup</c> and TLS needs no global set-up - so <see cref="CurlGlobal.Init"/>
/// accepts every value, as libcurl does, and keeps it on <see cref="CurlLibrary.Flags"/>.
/// </summary>
[Flags]
public enum CurlGlobalFlags : long
{
    /// <summary><c>CURL_GLOBAL_NOTHING</c> (0): initialise nothing extra.</summary>
    Nothing = 0,

    /// <summary><c>CURL_GLOBAL_SSL</c> (1): initialise TLS. Unused by libcurl 8.22.0, kept for source compatibility.</summary>
    Ssl = 1 << 0,

    /// <summary><c>CURL_GLOBAL_WIN32</c> (2): initialise the Win32 socket library.</summary>
    Win32 = 1 << 1,

    /// <summary><c>CURL_GLOBAL_ALL</c> (3): <see cref="Ssl"/> and <see cref="Win32"/>.</summary>
    All = Ssl | Win32,

    /// <summary><c>CURL_GLOBAL_DEFAULT</c> (3): the same as <see cref="All"/>.</summary>
    Default = All,

    /// <summary><c>CURL_GLOBAL_ACK_EINTR</c> (4): let an interrupted wait end early rather than resume.</summary>
    AckEintr = 1 << 2,
}
