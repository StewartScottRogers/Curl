using Curl.Api.Codes;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1918.c</c>: every row of the options table is looked up again by name (<c>curl_easy_option_by_name</c>) and by
/// id (<c>curl_easy_option_by_id</c>), and a lookup that finds another option writes
/// <c>name lookup id &lt;found&gt; does not match &lt;id&gt;</c> or <c>ID lookup &lt;found&gt; does not match &lt;id&gt;</c>
/// to stdout, the second printing the name lookup's id as upstream's does. The program ends with <c>CURLE_OK</c>.
/// </summary>
public sealed class Lib1918 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1918";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, LibTestSteps steps)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        using (StreamWriter output = LibTestRun.StandardOutputWriter(context))
        {
            CheckLookups(CurlOptions.All, CurlOptions.ByName, CurlOptions.ById, output);
        }

        steps.Result = CurlCode.Ok;
        return ValueTask.CompletedTask;
    }

    /// <summary>Looks each row up by name and by id and writes a line for each lookup that finds another option.</summary>
    /// <param name="rows">The options table's rows.</param>
    /// <param name="byName"><c>curl_easy_option_by_name</c>.</param>
    /// <param name="byId"><c>curl_easy_option_by_id</c>.</param>
    /// <param name="output">The program's stdout.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void CheckLookups(
        IEnumerable<CurlEasyOption> rows,
        Func<string, CurlEasyOption?> byName,
        Func<CurlOption, CurlEasyOption?> byId,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(byName);
        ArgumentNullException.ThrowIfNull(byId);
        ArgumentNullException.ThrowIfNull(output);
        foreach (CurlEasyOption row in rows)
        {
            CurlOption nameId = byName(row.Name)!.Id;
            if (nameId != row.Id)
            {
                output.Write($"name lookup id {(int)nameId} does not match {(int)row.Id}\n");
            }
            else if (byId(row.Id)!.Id != row.Id)
            {
                output.Write($"ID lookup {(int)nameId} does not match {(int)row.Id}\n");
            }
        }
    }
}
