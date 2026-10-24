namespace Curl.Conformance.Differential.Libcurl.Formadd;

/// <summary>
/// A form built with <c>curl_formadd</c> calls, each call's result code logged; when <paramref name="Sends"/>,
/// the form is then read with <c>curl_formget</c> and posted with <c>CURLOPT_HTTPPOST</c> to the loopback
/// server's <c>/echo</c>, and freed with <c>curl_formfree</c>.
/// </summary>
/// <param name="Name">The case's name.</param>
/// <param name="Calls">Each <c>curl_formadd</c> call's arguments, <c>CURLFORM_END</c> implied after them.</param>
/// <param name="Sends">Whether the form is read and posted; <see langword="false"/> for a stream field, which would read the handle's standard input.</param>
public sealed record LibcurlFormaddCase(string Name, IReadOnlyList<IReadOnlyList<LibcurlFormaddArgument>> Calls, bool Sends);
