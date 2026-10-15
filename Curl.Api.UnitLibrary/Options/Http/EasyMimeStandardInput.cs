namespace Curl.Api.Options.Http;

/// <summary>
/// A part's content read from standard input while the transfer sends it: the
/// <c>curl_mime_data_cb</c> part curl's tool sets for <c>-F name=@-</c> and
/// <c>-F name=&lt;-</c>, with <c>fread</c> and <c>fseek</c> on <c>stdin</c> as its
/// callbacks and an unknown size (-1), as <c>--libcurl</c> writes it.
/// </summary>
public sealed record EasyMimeStandardInput : EasyMimeData;
