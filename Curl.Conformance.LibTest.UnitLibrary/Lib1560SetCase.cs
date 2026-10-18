using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// One row of <c>lib1560.c</c>'s <c>set_parts_list</c> (<c>struct setcase</c>), <c>set_url_list</c>
/// (<c>struct redircase</c>, whose <paramref name="Set"/> is the URL set second) or <c>append_list</c>
/// (<c>struct querycase</c>, whose <paramref name="Set"/> is the query appended).
/// </summary>
/// <param name="In">The URL set first, or <see langword="null"/> to start from an empty handle.</param>
/// <param name="Set">The <c>part=value,</c> commands, the second URL or the query.</param>
/// <param name="Out">The whole URL wanted back.</param>
/// <param name="UrlFlags">The flags <paramref name="In"/> is set with.</param>
/// <param name="SetFlags">The flags <paramref name="Set"/> is set with.</param>
/// <param name="UrlCode">The code wanted from setting <paramref name="In"/> (from the append for <c>append_list</c>).</param>
/// <param name="PartCode">The code wanted from the part commands (<c>set_parts_list</c> only).</param>
public sealed record Lib1560SetCase(string? In, string Set, string Out, CurlUrlFlags UrlFlags, CurlUrlFlags SetFlags, CurlUrlCode UrlCode, CurlUrlCode PartCode);
