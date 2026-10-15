namespace Curl.Api.Options.Protocols;

/// <summary>
/// Whether FTP and SFTP create missing remote directories: libcurl's <c>CURLFTP_CREATE_DIR_*</c>
/// values for <c>CURLOPT_FTP_CREATE_MISSING_DIRS</c>, with the same numbers.
/// </summary>
public enum FtpCreateMissingDirectories
{
    /// <summary><c>CURLFTP_CREATE_DIR_NONE</c>: do not create directories (libcurl's default).</summary>
    None = 0,

    /// <summary><c>CURLFTP_CREATE_DIR</c>: create a directory when changing into it fails.</summary>
    Create = 1,

    /// <summary><c>CURLFTP_CREATE_DIR_RETRY</c>: as <see cref="Create"/>, and retry the change when creating fails.</summary>
    CreateAndRetry = 2,
}
