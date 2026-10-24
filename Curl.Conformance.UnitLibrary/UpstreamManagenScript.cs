using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli.ManualPages;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>scripts/managen</c> (curl 8.22.0), which cases 1705 and 1706 run as
/// <c>%SRCDIR/../scripts/managen -d %LOGDIR -I %SRCDIR/../include mainpage|ascii option1.md option2.md</c>, without
/// running Perl (BL-2881): Curl's own <see cref="ManualPageGenerator"/> writes the page from the case's files.
/// </summary>
/// <remarks>
/// The page's date is <c>SOURCE_DATE_EPOCH</c> from the invocation's environment as a UTC date, as the script reads
/// it, else today's local date from the time provider; <c>CURL_MAKETGZ_VERSION</c> overrides the version, as in the
/// script. Files are read and output written as Latin-1, so bytes pass through unchanged.
/// </remarks>
/// <param name="timeProvider">Gives today's date when the environment sets no <c>SOURCE_DATE_EPOCH</c>.</param>
public sealed class UpstreamManagenScript(TimeProvider timeProvider)
{
    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "managen";

    private static readonly string[] HeaderFieldNames = ["Short", "Long", "Arg", "Help", "Category"];

    // The long names whose entries upstream's script wraps in `#ifndef <macro>`, as the options their builds can disable.
    private static readonly Dictionary<string, string> DisabledBy = new(StringComparer.Ordinal)
    {
        ["httpsig-algo"] = "CURL_DISABLE_HTTPSIG",
        ["httpsig-headers"] = "CURL_DISABLE_HTTPSIG",
        ["httpsig-key"] = "CURL_DISABLE_HTTPSIG",
        ["httpsig-keyid"] = "CURL_DISABLE_HTTPSIG",
        ["ipfs-gateway"] = "CURL_DISABLE_IPFS",
    };

    private const int ListHelpLineWidth = 79;

    private const string ListHelpCommand = "listhelp";

    private const string ListHelpHeader = """
        /***************************************************************************
         *                                  _   _ ____  _
         *  Project                     ___| | | |  _ \| |
         *                             / __| | | | |_) | |
         *                            | (__| |_| |  _ <| |___
         *                             \___|\___/|_| \_\_____|
         *
         * Copyright (C) Daniel Stenberg, <daniel@haxx.se>, et al.
         *
         * This software is licensed as described in the file COPYING, which
         * you should have received as part of this distribution. The terms
         * are also available at https://curl.se/docs/copyright.html.
         *
         * You may opt to use, copy, modify, merge, publish, distribute and/or sell
         * copies of the Software, and permit persons to whom the Software is
         * furnished to do so, under the terms of the COPYING file.
         *
         * This software is distributed on an "AS IS" basis, WITHOUT WARRANTY OF ANY
         * KIND, either express or implied.
         *
         * SPDX-License-Identifier: curl
         *
         ***************************************************************************/
        #include "tool_setup.h"

        #include "tool_help.h"

        /*
         * DO NOT edit tool_listhelp.c manually.
         * This source file is generated with the following command in an autotools
         * build:
         *
         * "make listhelp"
         */

        const struct helptxt helptext[] = {

        """;

    /// <summary>Runs the script with the command's words after it.</summary>
    /// <param name="invocation">The script's arguments, environment and the case's streams.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        StringBuilder output = new();
        StringBuilder error = new();
        int exitCode = 0;
        IReadOnlyList<string> words = CommandWords(invocation.Arguments);
        if (words.FirstOrDefault() == ListHelpCommand)
        {
            output.Append(ListHelp(words.Skip(1)));
        }
        else
        {
            exitCode = new ManualPageGenerator(ReadFile, PageDate(invocation.EnvironmentVariables))
                .Run(invocation.Arguments, invocation.EnvironmentVariables.GetValueOrDefault("CURL_MAKETGZ_VERSION"), output, error);
        }

        await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(output.ToString()));
        await invocation.StandardError.WriteAsync(Encoding.Latin1.GetBytes(error.ToString()));
        return exitCode;
    }

    /// <summary>The page's date: <c>SOURCE_DATE_EPOCH</c> as a UTC date, else today's local date.</summary>
    /// <param name="environment">The invocation's environment.</param>
    /// <returns>The date, <c>yyyy-MM-dd</c>.</returns>
    public string PageDate(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        DateTimeOffset date = environment.TryGetValue("SOURCE_DATE_EPOCH", out string? epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(epoch, CultureInfo.InvariantCulture))
            : timeProvider.GetLocalNow();
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    // The words after the leading -d, -I and -c options: the command, then its files.
    private static IReadOnlyList<string> CommandWords(IReadOnlyList<string> arguments)
    {
        int index = 0;
        while (index < arguments.Count && arguments[index] is "-d" or "-I" or "-c")
        {
            index += 2;
        }

        return [.. arguments.Skip(index)];
    }

    // The script's `listhelp`: src/tool_listhelp.c for the option documents, sorted by long name.
    private string ListHelp(IEnumerable<string> files)
    {
        StringBuilder text = new(ListHelpHeader.ReplaceLineEndings("\n"));
        foreach (Dictionary<string, string> fields in files.SelectMany(ExpandWildcard).Select(ReadHeaderFields).Where(IsListed).OrderBy(fields => fields["Long"], StringComparer.Ordinal))
        {
            text.Append(ListHelpEntry(fields));
        }

        return text.Append("  { NULL, NULL, 0 }\n};\n").ToString();
    }

    // A document is listed when its header names a long option and its help text.
    private static bool IsListed(Dictionary<string, string> fields) => fields.ContainsKey("Long") && fields.ContainsKey("Help");

    private static string ListHelpEntry(Dictionary<string, string> fields)
    {
        string shortName = fields.TryGetValue("Short", out string? letter) ? $"-{letter}, " : "    ";
        string argument = fields.TryGetValue("Arg", out string? name) ? $" {name}" : "";
        string entry = $"  {{ \"{shortName}--{fields["Long"]}{argument}\",\n    \"{fields["Help"]}\",\n{WrapCategories(fields.GetValueOrDefault("Category", ""))}\n";
        return DisabledBy.TryGetValue(fields["Long"], out string? macro) ? $"#ifndef {macro}\n{entry}#endif\n" : entry;
    }

    // The `CURLHELP_X | CURLHELP_Y },` lines, wrapped before a flag that would take a line past ListHelpLineWidth.
    private static string WrapCategories(string categories)
    {
        string[] names = categories.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        StringBuilder lines = new("   ");
        int lineStart = 0;
        for (int index = 0; index < names.Length; index++)
        {
            string flag = $" CURLHELP_{names[index].ToUpperInvariant()}{(index == names.Length - 1 ? " }," : " |")}";
            if (lines.Length - lineStart + flag.Length > ListHelpLineWidth && lines.Length - lineStart > 3)
            {
                lines.Append("\n   ");
                lineStart = lines.Length - 3;
            }

            lines.Append(flag);
        }

        return names.Length == 0 ? "     }," : lines.ToString();
    }

    // A word naming files with `*` in its file name stands for them, as the shell would expand it; any other word stands for itself.
    private static IEnumerable<string> ExpandWildcard(string word) =>
        Path.GetFileName(word).Contains('*', StringComparison.Ordinal) && Directory.Exists(Path.GetDirectoryName(word))
            ? Directory.EnumerateFiles(Path.GetDirectoryName(word)!, Path.GetFileName(word)).Order(StringComparer.Ordinal)
            : [word];

    // The `Name: value` lines of a document's header block (between its first two "---" lines).
    private static Dictionary<string, string> ReadHeaderFields(string path)
    {
        Dictionary<string, string> fields = [];
        foreach (string line in HeaderBlock((ReadFile(path) ?? "").Split('\n')))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && HeaderFieldNames.Contains(line[..colon]))
            {
                fields[line[..colon]] = line[(colon + 1)..].Trim(' ', '\r');
            }
        }

        return fields;
    }

    // The lines between a document's first two "---" lines.
    private static IEnumerable<string> HeaderBlock(string[] lines) =>
        lines.SkipWhile(line => !line.StartsWith("---", StringComparison.Ordinal)).Skip(1).TakeWhile(line => !line.StartsWith("---", StringComparison.Ordinal));

    private static string? ReadFile(string path) => File.Exists(path) ? Encoding.Latin1.GetString(File.ReadAllBytes(path)) : null;
}
