using Curl.Api.Codes;
using Curl.Api.Options;

namespace Libcurl.Options;

/// <summary>
/// Sets one option on a handle's <see cref="EasyOptions"/>: the row an option group adds to
/// <see cref="EasyOptionSetters"/>. Called only with a value of the option's <see cref="CurlOptionType"/>.
/// </summary>
/// <param name="options">The handle's options, replaced by the changed copy on success.</param>
/// <param name="value">The value given to <c>SetOption</c>.</param>
/// <returns><see cref="CurlCode.Ok"/>, or the code <c>curl_easy_setopt</c> answers for the value.</returns>
internal delegate CurlCode EasyOptionSetter(ref EasyOptions options, object? value);
