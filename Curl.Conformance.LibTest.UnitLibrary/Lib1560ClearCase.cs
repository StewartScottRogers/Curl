using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <summary>One row of <c>lib1560.c</c>'s <c>clear_url_list</c> (<c>struct clearurlcase</c>): a part set, then cleared with the whole URL.</summary>
/// <param name="Part">The part set and read back.</param>
/// <param name="In">The part's text.</param>
/// <param name="Out">The text wanted back after the clear, or <see langword="null"/> for none.</param>
/// <param name="Code">The code wanted from reading the part back.</param>
public sealed record Lib1560ClearCase(CurlUrlPart Part, string In, string? Out, CurlUrlCode Code);
