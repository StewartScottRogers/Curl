using System.Collections.Frozen;
using System.Globalization;

namespace Curl.Config;

/// <summary>
/// <c>curl-config</c>'s options, as curl 8.22.0's <c>curl-config.in</c> answers them and ADR-0712
/// fills them in: each option in turn prints its answer, one value per line ending in a line feed;
/// <c>--version</c>, <c>--vernum</c>, <c>--help</c> and <c>--checkfor</c> end the run, as does an
/// unknown option, which prints <c>unknown option: </c> and the usage and exits 1. No option prints
/// the usage and exits 1. Everything goes to standard output, as the script writes it.
/// </summary>
public static class CurlConfigCommand
{
    private delegate int? Answer(CurlConfigBuild build, string nextArgument, TextWriter output);

    private static readonly FrozenDictionary<string, Answer> Answers = new Dictionary<string, Answer>(StringComparer.Ordinal)
    {
        ["--built-shared"] = (_, _, output) => Line(output, "no"),
        ["--ca"] = (build, _, output) => Line(output, build.CaBundle),
        ["--cc"] = (_, _, output) => Line(output, "dotnet"),
        ["--cflags"] = (_, _, output) => Line(output, ""),
        ["--checkfor"] = (build, nextArgument, output) => CurlConfigVersionCheck.Run(nextArgument, build.Version, output),
        ["--configure"] = (_, _, output) => Line(output, ""),
        ["--feature"] = (build, _, output) => Lines(output, build.Features),
        ["--features"] = (build, _, output) => Lines(output, build.Features),
        ["--help"] = (_, _, output) => Usage(output, 0),
        ["--libs"] = (_, _, output) => Line(output, ""),
        ["--prefix"] = (build, _, output) => Line(output, build.Prefix),
        ["--protocols"] = (build, _, output) => Lines(output, build.Protocols),
        ["--ssl-backends"] = (build, _, output) => Line(output, build.SslBackends),
        ["--static-libs"] = (_, _, output) => Line(output, ""),
        ["--version"] = (build, _, output) => Final(output, $"libcurl {build.Version}"),
        ["--vernum"] = (build, _, output) => Final(output, build.VersionNumber.ToString("x6", CultureInfo.InvariantCulture)),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The usage <c>--help</c> prints, word for word as curl 8.22.0's <c>curl-config.in</c>.</summary>
    public const string UsageText =
        "Usage: curl-config [OPTION]\n" +
        "\n" +
        "Available values for OPTION include:\n" +
        "\n" +
        "  --built-shared        says 'yes' if libcurl was built shared\n" +
        "  --ca                  CA bundle install path\n" +
        "  --cc                  compiler\n" +
        "  --cflags              preprocessor and compiler flags\n" +
        "  --checkfor [version]  check for (lib)curl of the specified version\n" +
        "  --configure           the arguments given to configure when building curl\n" +
        "  --features            newline separated list of enabled features\n" +
        "  --help                display this help and exit\n" +
        "  --libs                library linking information\n" +
        "  --prefix              curl install prefix\n" +
        "  --protocols           newline separated list of enabled protocols\n" +
        "  --ssl-backends        output the SSL backends libcurl was built to support\n" +
        "  --static-libs         static libcurl library linking information\n" +
        "  --version             output version information\n" +
        "  --vernum              output version as a hexadecimal number\n";

    /// <summary>Answers the options in <paramref name="arguments"/>, in order.</summary>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="build">What the build reports.</param>
    /// <param name="output">Standard output.</param>
    /// <returns>The exit code: 0, or 1 for no option, an unknown option or a failed <c>--checkfor</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int Run(IReadOnlyList<string> arguments, CurlConfigBuild build, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(output);
        if (arguments.Count == 0)
        {
            return Usage(output, 1);
        }

        for (int index = 0; index < arguments.Count; index++)
        {
            if (AnswerOne(arguments[index], index + 1 < arguments.Count ? arguments[index + 1] : "", build, output) is int exitCode)
            {
                return exitCode;
            }
        }

        return 0;
    }

    private static int? AnswerOne(string option, string nextArgument, CurlConfigBuild build, TextWriter output) =>
        Answers.TryGetValue(option, out Answer? answer) ? answer(build, nextArgument, output) : Unknown(option, output);

    private static int Unknown(string option, TextWriter output)
    {
        _ = Line(output, $"unknown option: {option}");
        return Usage(output, 1);
    }

    private static int Final(TextWriter output, string text)
    {
        _ = Line(output, text);
        return 0;
    }

    private static int Usage(TextWriter output, int exitCode)
    {
        output.Write(UsageText);
        return exitCode;
    }

    private static int? Line(TextWriter output, string text)
    {
        output.Write(text + "\n");
        return null;
    }

    private static int? Lines(TextWriter output, IReadOnlyList<string> values)
    {
        foreach (string value in values)
        {
            output.Write(value + "\n");
        }

        return null;
    }
}
