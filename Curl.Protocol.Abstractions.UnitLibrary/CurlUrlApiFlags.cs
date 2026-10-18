namespace Curl.Protocol.Abstractions;

/// <summary>
/// The libcurl URL API flags <see cref="CurlUrl.TryParseForUrlApi(string, bool, CurlUrlApiFlags, out CurlUrl, out CurlUrlRejection)" /> reads.
/// </summary>
/// <param name="DisallowUser"><c>CURLU_DISALLOW_USER</c>: user information is rejected.</param>
/// <param name="AllowEmptyHost"><c>CURLU_NO_AUTHORITY</c>: the authority may be empty.</param>
/// <param name="GuessesScheme">
/// <c>CURLU_GUESS_SCHEME</c> or <c>CURLU_DEFAULT_SCHEME</c>: a scheme needs <c>:/</c> after it, as
/// <c>Curl_is_absolute_url</c> asks when it guesses.
/// </param>
public readonly record struct CurlUrlApiFlags(bool DisallowUser, bool AllowEmptyHost, bool GuessesScheme);
