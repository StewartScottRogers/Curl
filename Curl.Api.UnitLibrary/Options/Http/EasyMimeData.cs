namespace Curl.Api.Options.Http;

/// <summary>
/// Where an <see cref="EasyMimePart"/>'s content comes from: exactly one of
/// <see cref="EasyMimeBytes"/> (<c>curl_mime_data</c>), <see cref="EasyMimeFile"/>
/// (<c>curl_mime_filedata</c>), <see cref="EasyMimeCallback"/>
/// (<c>curl_mime_data_cb</c>) or <see cref="EasyMimeStandardInput"/> (curl's tool's
/// <c>curl_mime_data_cb</c> on standard input).
/// </summary>
public abstract record EasyMimeData;
