using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// Curl's manual page generator, the C# counterpart of upstream's <c>scripts/managen</c> at curl 8.22.0 (BL-2881):
/// from a folder's <c>mainpage.idx</c> and option documents (Markdown after a header block) it writes the nroff man
/// page (<c>mainpage</c>) or the ASCII page (<c>ascii</c>), with the script's warnings, errors and exit codes.
/// </summary>
/// <remarks>
/// The arguments are the script's: <c>-d DIR</c>, <c>-I INCLUDE</c> (whose <c>curl/curlver.h</c> gives the version
/// unless one is passed) and <c>-c COLUMNS</c>, then the command and the option documents. Only <c>mainpage</c> and
/// <c>ascii</c> are carried out; any other command (the script's <c>listhelp</c>, <c>single</c>, <c>protos</c> and
/// <c>listcats</c> included) writes the script's usage line. A <c>die</c> writes its message without Perl's
/// <c>at ... line N.</c> suffix and exits 2, the code an <c>open</c> failure's <c>die</c> exits with.
/// </remarks>
/// <param name="readFile">Reads a file's text by path, or returns <see langword="null"/> when there is none.</param>
/// <param name="date">The date the man page is stamped with, <c>yyyy-MM-dd</c>.</param>
public sealed class ManualPageGenerator(Func<string, string?> readFile, string date)
{
    /// <summary>Runs the generator.</summary>
    /// <param name="arguments">The script's arguments.</param>
    /// <param name="version">The version to name (the script's <c>CURL_MAKETGZ_VERSION</c>), or <see langword="null"/> to read <c>curlver.h</c>.</param>
    /// <param name="output">Receives standard output.</param>
    /// <param name="error">Receives standard error.</param>
    /// <returns>The script's exit code.</returns>
    public int Run(IReadOnlyList<string> arguments, string? version, StringBuilder output, StringBuilder error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ManualPageContext context = new(readFile, date, output, error);
        try
        {
            return Run(context, new Queue<string>(arguments), version);
        }
        catch (ManualPageExit exit)
        {
            return exit.ExitCode;
        }
    }

    private static int Run(ManualPageContext context, Queue<string> arguments, string? version)
    {
        string include = "../../include";
        string command = Shift(arguments);
        for (; command is "-d" or "-I" or "-c"; command = Shift(arguments))
        {
            string value = Shift(arguments);
            context.Directory = command == "-d" ? value : context.Directory;
            include = command == "-I" ? value : include;
            context.ColumnWidth = command == "-c" ? ManualPerlValues.Number(value) : context.ColumnWidth;
        }

        List<string> files = [.. arguments];
        context.Version = ManualPerlValues.IsTrue(version) ? version : ReadVersion(context, include);
        files.ForEach(file => context.Index.Add(context, file));
        if (command is "mainpage" or "ascii")
        {
            ListGlobals(context, files);
            MainPage(context, command == "mainpage", files);
        }
        else
        {
            context.Output.Append("Usage: managen [-d dir] <mainpage/ascii/listhelp/single FILE/protos/listcats> [files]\n");
        }

        return context.Errors;
    }

    private static string Shift(Queue<string> arguments) => arguments.TryDequeue(out string? argument) ? argument : "";

    private static string ReadVersion(ManualPageContext context, string include)
    {
        string header = $"{include}/curl/curlver.h";
        return context.ReadLines(header, $"no {header}")
            .Select(line => Regex.Match(line, "^#define LIBCURL_VERSION \"([0-9.]*)", RegexOptions.CultureInvariant))
            .FirstOrDefault(match => match.Success)?.Groups[1].Value ?? "unknown";
    }

    // The global options' names, for `%GLOBALS`: "--a, --b and --c".
    private static void ListGlobals(ManualPageContext context, List<string> files)
    {
        List<string> globals = [];
        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            string? longName = null;
            foreach (string line in context.ReadLines(context.PathOf(file), $"could not read {context.PathOf(file)}").SkipWhile(line => !line.StartsWith("---", StringComparison.Ordinal)).Skip(1)
                .TakeWhile(line => !line.StartsWith("---", StringComparison.Ordinal)))
            {
                longName = Regex.Match(line, "^Long: *(.*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) is { Success: true } match ? match.Groups[1].Value : longName;
                if (Regex.IsMatch(line, "^Scope: global", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    globals.Add(longName ?? "");
                    break;
                }
            }
        }

        context.Globals = string.Concat(globals.Select((name, index) =>
            (index == 0 ? "" : index + 1 < globals.Count && ManualPerlValues.IsTrue(globals[index + 1]) ? ", " : " and ") + $"--{name}"));
    }

    private static void MainPage(ManualPageContext context, bool manpage, List<string> files)
    {
        List<string> index = context.ReadLines(context.PathOf("mainpage.idx"), $"no {context.PathOf("mainpage.idx")} file");
        if (manpage)
        {
            context.Output.Append(ManHeader(context));
        }

        int failures = 0;
        foreach (string line in index.Where(line => !line.StartsWith('#')))
        {
            failures += line.StartsWith("%options", StringComparison.Ordinal)
                ? files.OrderBy(NoExtension, StringComparer.Ordinal).Sum(file => new ManualOptionPage(context, manpage).Write(file))
                : WriteTopLevelPage(context, manpage, ManualPerlValues.Chomp(line));
        }

        if (failures != 0)
        {
            throw new ManualPageExit(failures & 0xFF);
        }
    }

    private static int WriteTopLevelPage(ManualPageContext context, bool manpage, string fileName)
    {
        List<string> lines = context.ReadLines(context.PathOf(fileName), $"could not find {context.PathOf(fileName)}");
        new ManualAsciiLayout(context).PrintDescription(manpage, 0, new ManualMarkdownRenderer(context, manpage, fileName, 1).Render(lines));
        return 0;
    }

    private static string NoExtension(string file) => new Regex(@"\.md", RegexOptions.CultureInvariant).Replace(file, "", 1);

    private static string ManHeader(ManualPageContext context) => $$"""
        .\" **************************************************************************
        .\" *                                  _   _ ____  _
        .\" *  Project                     ___| | | |  _ \| |
        .\" *                             / __| | | | |_) | |
        .\" *                            | (__| |_| |  _ <| |___
        .\" *                             \___|\___/|_| \_\_____|
        .\" *
        .\" * Copyright (C) Daniel Stenberg, <daniel@haxx.se>, et al.
        .\" *
        .\" * This software is licensed as described in the file COPYING, which
        .\" * you should have received as part of this distribution. The terms
        .\" * are also available at https://curl.se/docs/copyright.html.
        .\" *
        .\" * You may opt to use, copy, modify, merge, publish, distribute and/or sell
        .\" * copies of the Software, and permit persons to whom the Software is
        .\" * furnished to do so, under the terms of the COPYING file.
        .\" *
        .\" * This software is distributed on an "AS IS" basis, WITHOUT WARRANTY OF ANY
        .\" * KIND, either express or implied.
        .\" *
        .\" * SPDX-License-Identifier: curl
        .\" *
        .\" **************************************************************************
        .\"
        .\" DO NOT EDIT. Generated by the curl project managen man page generator.
        .\"
        .TH curl 1 "{{context.Date}}" "curl {{context.Version}}" "curl Manual"

        """.ReplaceLineEndings("\n");
}
