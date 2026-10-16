namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_TRAILERFUNCTION</c>: supplies the trailer headers of a chunked HTTP upload.
/// <c>CURLOPT_TRAILERDATA</c> is the delegate's closure.
/// </summary>
/// <param name="trailers">The list to add <c>Name: value</c> trailer lines to (upstream's <c>curl_slist **</c>).</param>
/// <returns>Whether to send the trailers or abort.</returns>
public delegate TrailerResult TrailerCallback(IList<string> trailers);
