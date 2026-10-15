namespace Curl.Api.Options.Http;

/// <summary>
/// One part of an <see cref="EasyMimeBody"/>: the data model of a libcurl
/// <c>curl_mimepart</c>, as the <c>curl_mime_*</c> setters leave it. Every default is
/// that of a part fresh from <c>curl_mime_addpart</c>.
/// </summary>
public sealed record EasyMimePart
{
    /// <summary>
    /// <c>curl_mime_name</c>: the form field name. Default <see langword="null"/>: none.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// <c>curl_mime_filename</c>: the remote file name sent in <c>Content-Disposition</c>.
    /// Default <see langword="null"/>: none (<c>curl_mime_filedata</c> sets it to the file's
    /// base name).
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// <c>curl_mime_type</c>: the <c>Content-Type</c>. Default <see langword="null"/>: libcurl
    /// derives one from the file name or the data source.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// <c>curl_mime_encoder</c>: the transfer encoding - <c>binary</c>, <c>8bit</c>,
    /// <c>7bit</c>, <c>base64</c> or <c>quoted-printable</c>. Default
    /// <see langword="null"/>: the data is sent as it is, with no
    /// <c>Content-Transfer-Encoding</c>.
    /// </summary>
    public string? Encoder { get; init; }

    /// <summary>
    /// <c>curl_mime_headers</c>: the part's extra headers. Default empty.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>
    /// <c>curl_mime_subparts</c>: a nested multipart body sent as this part's content.
    /// Default <see langword="null"/>: none.
    /// </summary>
    public EasyMimeBody? SubParts { get; init; }

    /// <summary>
    /// <c>curl_mime_data</c>, <c>curl_mime_filedata</c> or <c>curl_mime_data_cb</c>: where
    /// the part's content comes from. Default <see langword="null"/>: no data, an empty part.
    /// </summary>
    public EasyMimeData? Data { get; init; }
}
