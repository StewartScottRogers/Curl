namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>One <c>curl_easy_setopt</c> call of a round.</summary>
/// <param name="Id">The <c>CURLoption</c> number.</param>
/// <param name="Kind">How the value is passed.</param>
/// <param name="Number">The value of a <see cref="LibcurlEasyOptionKind.Number"/> option.</param>
/// <param name="Text">The value of a <see cref="LibcurlEasyOptionKind.Text"/> option; <see langword="null"/> passes <c>NULL</c>.</param>
/// <param name="List">The strings of a <see cref="LibcurlEasyOptionKind.List"/> option.</param>
public sealed record LibcurlEasyOption(int Id, LibcurlEasyOptionKind Kind, long Number, string? Text, IReadOnlyList<string> List);
