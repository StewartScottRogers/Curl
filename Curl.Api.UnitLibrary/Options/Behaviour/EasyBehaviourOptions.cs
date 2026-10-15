using System.Text;

namespace Curl.Api.Options.Behaviour;

/// <summary>
/// An easy handle's behaviour options (ADR-0506, plan row <c>EasyBehaviourOptions</c>): verbose
/// output and where it goes, headers in the body, the progress meter, signals, FTP wildcard
/// matching, the error buffer, failing on HTTP errors, the private pointer, the share handle,
/// quick exit and the permissions of files and directories libcurl creates. Every default is
/// libcurl 8.22.0's.
/// </summary>
public sealed record EasyBehaviourOptions
{
    /// <summary>
    /// <c>CURLOPT_VERBOSE</c>: whether libcurl writes verbose information about the transfer to
    /// <see cref="StandardError"/> (or the debug callback). Default <see langword="false"/> (0).
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// <c>CURLOPT_HEADER</c>: whether the response headers are passed to the write callback with
    /// the body. Default <see langword="false"/> (0).
    /// </summary>
    public bool IncludeHeader { get; init; }

    /// <summary>
    /// <c>CURLOPT_NOPROGRESS</c>: whether the progress meter and the progress callbacks are
    /// switched off. Default <see langword="true"/> (1).
    /// </summary>
    public bool NoProgress { get; init; } = true;

    /// <summary>
    /// <c>CURLOPT_NOSIGNAL</c>: whether libcurl avoids every signal-raising call, such as the
    /// <c>SIGALRM</c> timeout of a synchronous name resolve. Default <see langword="false"/> (0).
    /// </summary>
    public bool NoSignal { get; init; }

    /// <summary>
    /// <c>CURLOPT_WILDCARDMATCH</c>: whether the last part of an FTP URL is a file name pattern
    /// that downloads every matching file. Default <see langword="false"/> (0).
    /// </summary>
    public bool WildcardMatch { get; init; }

    /// <summary>
    /// <c>CURLOPT_ERRORBUFFER</c>: the buffer a failed call writes its human-readable error
    /// message into, cut to <c>CURL_ERROR_SIZE</c> (256) bytes with its terminator. Default
    /// <see langword="null"/>: no message is kept.
    /// </summary>
    public StringBuilder? ErrorBuffer { get; init; }

    /// <summary>
    /// <c>CURLOPT_STDERR</c>: the stream verbose output and the progress meter are written to.
    /// Default <see langword="null"/>: the process's standard error.
    /// </summary>
    public Stream? StandardError { get; init; }

    /// <summary>
    /// <c>CURLOPT_FAILONERROR</c>: whether an HTTP response code of 400 or more fails the transfer
    /// with <c>CURLE_HTTP_RETURNED_ERROR</c> instead of delivering the body. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool FailOnError { get; init; }

    /// <summary>
    /// <c>CURLOPT_KEEP_SENDING_ON_ERROR</c>: whether an upload keeps sending after an HTTP
    /// response code of 300 or more arrives. Default <see langword="false"/> (0).
    /// </summary>
    public bool KeepSendingOnError { get; init; }

    /// <summary>
    /// <c>CURLOPT_PRIVATE</c>: an application value kept with the handle and read back with
    /// <c>CURLINFO_PRIVATE</c>; libcurl never reads it. Default <see langword="null"/>.
    /// </summary>
    public object? Private { get; init; }

    /// <summary>
    /// <c>CURLOPT_SHARE</c>: the share handle whose cookies, DNS cache, TLS sessions, connections,
    /// public suffix list or HSTS cache the transfer shares. Default <see langword="null"/>:
    /// nothing is shared.
    /// </summary>
    public object? Share { get; init; }

    /// <summary>
    /// <c>CURLOPT_QUICK_EXIT</c>: whether the handle may leak resources rather than wait for a
    /// slow threaded name resolve when it is cleaned up, so the process can exit quickly. Default
    /// <see langword="false"/> (0).
    /// </summary>
    public bool QuickExit { get; init; }

    /// <summary>
    /// <c>CURLOPT_NEW_FILE_PERMS</c>: the permissions of a file libcurl creates on the server (SFTP,
    /// SCP and <c>file:</c>). Default 0644 (owner read and write, group and others read).
    /// </summary>
    public UnixFileMode NewFilePermissions { get; init; } =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    /// <summary>
    /// <c>CURLOPT_NEW_DIRECTORY_PERMS</c>: the permissions of a directory libcurl creates on the
    /// server (SFTP and <c>file:</c>). Default 0755 (owner all, group and others read and execute).
    /// </summary>
    public UnixFileMode NewDirectoryPermissions { get; init; } =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
}
