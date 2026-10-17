namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>curl.h</c>'s <c>CURLOPT_IOCTLFUNCTION</c> values, which <c>lib547.c</c> and <c>lib552.c</c> set: the
/// command libcurl passes and the answers the callback gives. The callback is handed to
/// <c>curl_easy_setopt</c> as a <see cref="Func{T, TResult}"/> from command to answer; Libcurl has no
/// <c>CURLOPT_IOCTLFUNCTION</c> yet and answers <c>CURLE_NOT_BUILT_IN</c> for it.
/// </summary>
public static class LibTestIoctl
{
    /// <summary><c>CURLIOCMD_RESTARTREAD</c>: restart the read stream from the start.</summary>
    public const int RestartRead = 1;

    /// <summary><c>CURLIOE_OK</c>: the command was done.</summary>
    public const int Ok = 0;

    /// <summary><c>CURLIOE_UNKNOWNCMD</c>: the command is not known.</summary>
    public const int UnknownCommand = 1;
}
