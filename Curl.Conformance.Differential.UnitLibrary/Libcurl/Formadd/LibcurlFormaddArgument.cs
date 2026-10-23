namespace Curl.Conformance.Differential.Libcurl.Formadd;

/// <summary>One option and value pair of a <c>curl_formadd</c> call.</summary>
/// <param name="Option">The <c>CURLFORM_*</c> option's number in curl 8.22.0's <c>curl.h</c>, kept as a number so unknown options can be passed.</param>
/// <param name="Kind">What the value is.</param>
/// <param name="Text">The string, hexadecimal bytes or file name of a <see cref="LibcurlFormaddValueKind.Text"/>, <see cref="LibcurlFormaddValueKind.Bytes"/> or <see cref="LibcurlFormaddValueKind.File"/> value.</param>
/// <param name="Number">The length of a <see cref="LibcurlFormaddValueKind.Length"/> value.</param>
/// <param name="Lines">The header lines of a <see cref="LibcurlFormaddValueKind.Headers"/> value.</param>
/// <param name="Array">The pairs of a <see cref="LibcurlFormaddValueKind.Array"/> value.</param>
public sealed record LibcurlFormaddArgument(int Option, LibcurlFormaddValueKind Kind, string? Text, long Number, IReadOnlyList<string> Lines, IReadOnlyList<LibcurlFormaddArgument> Array);
