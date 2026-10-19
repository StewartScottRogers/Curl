using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <summary>One row of <c>lib1560.c</c>'s <c>setget_parts_list</c> (<c>struct setgetcase</c>): a URL, part commands, then every part read back.</summary>
/// <param name="In">The URL set first, or <see langword="null"/> to start from an empty handle.</param>
/// <param name="Set">The <c>part=value,</c> commands.</param>
/// <param name="Out">The parts wanted back (<c>checkparts</c>' line).</param>
/// <param name="UrlFlags">The flags <paramref name="In"/> is set with.</param>
/// <param name="SetFlags">The flags the parts are set with.</param>
/// <param name="GetFlags">The flags the parts are read with.</param>
/// <param name="PartCode">The code wanted from the part commands.</param>
public sealed record Lib1560SetGetCase(string? In, string Set, string Out, CurlUrlFlags UrlFlags, CurlUrlFlags SetFlags, CurlUrlFlags GetFlags, CurlUrlCode PartCode);
