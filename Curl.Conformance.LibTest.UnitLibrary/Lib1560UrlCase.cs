using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// One row of <c>lib1560.c</c>'s <c>get_parts_list</c> (<c>struct testcase</c>) or <c>get_url_list</c>
/// (<c>struct urltestcase</c>): a URL set with <paramref name="UrlFlags"/>, read back with <paramref name="GetFlags"/>.
/// </summary>
/// <param name="In">The URL set.</param>
/// <param name="Out">The parts (<c>checkparts</c>' <c>a | b | [16]</c> line) or the whole URL wanted back.</param>
/// <param name="UrlFlags">The flags the URL is set with.</param>
/// <param name="GetFlags">The flags each part or the URL is read with.</param>
/// <param name="Code">The code wanted from the set (<c>get_parts</c>) or the get (<c>get_url</c>).</param>
/// <param name="WindowsOnly">Whether the row is inside upstream's <c>#ifdef _WIN32</c>.</param>
public sealed record Lib1560UrlCase(string In, string Out, CurlUrlFlags UrlFlags, CurlUrlFlags GetFlags, CurlUrlCode Code, bool WindowsOnly = false);
