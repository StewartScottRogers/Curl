namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>An option and its value as a multi or share case names it: <c>[id, kind, value]</c>.</summary>
/// <param name="Id">The <c>CURLOPT_*</c>, <c>CURLMOPT_*</c> or <c>CURLSHOPT_*</c> number.</param>
/// <param name="Kind"><c>l</c> for a number, <c>s</c> for text, <c>share</c> for the case's share handle, <c>null</c> for a null value.</param>
/// <param name="Number">The number, when <paramref name="Kind"/> is <c>l</c>.</param>
/// <param name="Text">The text, when <paramref name="Kind"/> is <c>s</c>.</param>
public sealed record LibcurlMultiValue(int Id, string Kind, long Number, string? Text);
