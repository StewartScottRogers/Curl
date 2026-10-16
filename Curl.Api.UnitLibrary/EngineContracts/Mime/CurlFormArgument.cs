namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// One option and value pair of a <c>curl_formadd</c> call. The value's type follows the option:
/// a <see cref="string"/> for names, file names, types and text contents, a <see cref="byte"/>
/// array for binary contents and buffers, a <see cref="long"/> for lengths, an
/// <see cref="IReadOnlyList{T}"/> of <see cref="string"/> for <see cref="CurlFormOption.ContentHeader"/>,
/// an <see cref="IReadOnlyList{T}"/> of <see cref="CurlFormArgument"/> for
/// <see cref="CurlFormOption.Array"/>, and any object for <see cref="CurlFormOption.Stream"/>.
/// </summary>
/// <param name="Option">The <c>CURLFORM_*</c> option.</param>
/// <param name="Value">The option's value.</param>
public sealed record CurlFormArgument(CurlFormOption Option, object? Value);
