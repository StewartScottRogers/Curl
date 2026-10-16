using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.Multi;

/// <summary>
/// Sets one option on a multi handle's <see cref="MultiOptions"/>: a row of <see cref="MultiOptionSetters"/>.
/// Called only with a value of the option's type.
/// </summary>
/// <param name="options">The handle's options, replaced by the changed copy on success.</param>
/// <param name="value">The value given to <see cref="CurlMulti.SetOption"/>.</param>
/// <returns><see cref="CurlMultiCode.Ok"/>, or the code <c>curl_multi_setopt</c> answers for the value.</returns>
internal delegate CurlMultiCode MultiOptionSetter(ref MultiOptions options, object? value);
