using Curl.Conformance.UnitProgram.Hashes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Strings;

namespace Curl.Conformance.UnitProgram.Limits;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1605.c</c>: negative data lengths given to libcurl's string
/// functions, here <see cref="CurlStrings.Escape(CurlEasy?, string?, int)"/> and
/// <see cref="CurlStrings.Unescape(CurlEasy?, string?, int, out int)"/> on a handle from
/// <see cref="CurlGlobal.Init(CurlGlobalFlags)"/>. Its two checks, in order: <c>curl_easy_escape</c> of the
/// empty string with length -1 answers <c>NULL</c>, and <c>curl_easy_unescape</c> of <c>%41%41%41%41</c>
/// with length -1 answers <c>NULL</c>.
/// </summary>
/// <param name="escape"><c>curl_easy_escape</c>; <see cref="CurlStrings.Escape(CurlEasy?, string?, int)"/> unless a test injects another.</param>
/// <param name="unescape"><c>curl_easy_unescape</c>; <see cref="CurlStrings.Unescape(CurlEasy?, string?, int, out int)"/> unless a test injects another.</param>
public sealed class Unit1605NegativeLengths(
    Func<CurlEasy, string, int, string?> escape,
    Func<CurlEasy, string, int, byte[]?> unescape) : IUnitProgram
{
    /// <summary>Creates the port over <see cref="CurlStrings"/>.</summary>
    public Unit1605NegativeLengths()
        : this(CurlStrings.Escape, (handle, text, length) => CurlStrings.Unescape(handle, text, length, out _))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit1605";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.All);
        await using CurlEasy easy = library.EasyInit();
        checks.FailUnless(escape(easy, string.Empty, -1) is null, "negative string length cannot work");
        checks.FailUnless(unescape(easy, "%41%41%41%41", -1) is null, "negative string length cannot work");
        return checks.Result;
    }
}
