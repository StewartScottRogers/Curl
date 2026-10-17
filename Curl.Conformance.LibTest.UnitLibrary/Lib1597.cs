using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Version;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1597.c</c>: <c>CURLOPT_PROTOCOLS_STR</c> set to each string of <see cref="Cases"/> in turn, each answer held to
/// the code it expects; <c>http</c> and <c>https</c> are expected to succeed only when <c>curl_version_info</c> lists them,
/// and the list of every enabled protocol (each after a comma) always. The first unexpected answer writes
/// <c>unexpectedly 'S' returned N</c> on stdout and stops; <c>Tested N strings</c> follows on stdout either way, and the
/// last answer is the program's code.
/// </summary>
public sealed class Lib1597 : ILibTest
{
    /// <summary>
    /// The program's <c>prots</c> table, in order: each string and its expected code, where <see langword="null"/> is
    /// the enabled-protocol list and the codes <c>httpcode</c> and <c>httpscode</c> are named by <see cref="Lib1597Expectation"/>.
    /// </summary>
    public static IReadOnlyList<(string? Text, Lib1597Expectation Expected)> Cases { get; } =
    [
        ("goobar", Lib1597Expectation.Unsupported),
        ("http ", Lib1597Expectation.Unsupported),
        (" http", Lib1597Expectation.Unsupported),
        ("http", Lib1597Expectation.Http),
        ("http,", Lib1597Expectation.Http),
        ("https,", Lib1597Expectation.Https),
        ("https,http", Lib1597Expectation.Https),
        ("http,http", Lib1597Expectation.Http),
        ("HTTP,HTTP", Lib1597Expectation.Http),
        (",HTTP,HTTP", Lib1597Expectation.Http),
        ("http,http,ft", Lib1597Expectation.Unsupported),
        (string.Empty, Lib1597Expectation.BadArgument),
        (",,", Lib1597Expectation.BadArgument),
        (null, Lib1597Expectation.Ok),
        ("all", Lib1597Expectation.Ok),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1597";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            steps.Result = TestStrings(CurlVersion.Info(library).Protocols, value => easy.SetOption(CurlOption.ProtocolsStr, value), output);
        }
    }

    /// <summary>
    /// The program's loop over <see cref="Cases"/>: each string set with <paramref name="setProtocols"/> until one answers
    /// other than expected, which writes <c>unexpectedly 'S' returned N</c>; then <c>Tested N strings</c>, N the strings
    /// before the unexpected one.
    /// </summary>
    /// <param name="protocols">The enabled protocols, as <c>curl_version_info</c> lists them.</param>
    /// <param name="setProtocols">Sets <c>CURLOPT_PROTOCOLS_STR</c> and answers its code.</param>
    /// <param name="output">The program's stdout.</param>
    /// <returns>The last answer, the program's <c>result</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CurlCode TestStrings(IReadOnlyList<string> protocols, Func<string, CurlCode> setProtocols, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(protocols);
        ArgumentNullException.ThrowIfNull(setProtocols);
        ArgumentNullException.ThrowIfNull(output);
        string protocolList = string.Concat(protocols.Select(protocol => "," + protocol));
        CurlCode result = CurlCode.Ok;
        int tested = 0;
        foreach (var (text, expected) in Cases)
        {
            string value = text ?? protocolList;
            result = setProtocols(value);
            if (result != CodeFor(expected, protocols))
            {
                output.Write($"unexpectedly '{value}' returned {(int)result}\n");
                break;
            }

            tested++;
        }

        output.Write($"Tested {tested} strings\n");
        return result;
    }

    /// <summary>The code <paramref name="expected"/> stands for when <paramref name="protocols"/> are enabled.</summary>
    /// <param name="expected">The expectation.</param>
    /// <param name="protocols">The enabled protocols, as <c>curl_version_info</c> lists them.</param>
    /// <returns>The expected code.</returns>
    public static CurlCode CodeFor(Lib1597Expectation expected, IReadOnlyList<string> protocols) => expected switch
    {
        Lib1597Expectation.Ok => CurlCode.Ok,
        Lib1597Expectation.BadArgument => CurlCode.BadFunctionArgument,
        Lib1597Expectation.Http => Enabled(protocols, "http"),
        Lib1597Expectation.Https => Enabled(protocols, "https"),
        _ => CurlCode.UnsupportedProtocol,
    };

    private static CurlCode Enabled(IReadOnlyList<string> protocols, string name) =>
        protocols.Any(protocol => string.Equals(protocol, name, StringComparison.OrdinalIgnoreCase)) ? CurlCode.Ok : CurlCode.UnsupportedProtocol;
}
