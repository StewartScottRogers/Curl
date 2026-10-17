using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib576.c</c>: an FTP wildcard download whose <c>CURLOPT_CHUNK_BGN_FUNCTION</c> and
/// <c>CURLOPT_CHUNK_END_FUNCTION</c> (<see cref="ChunkPrinter"/>) print each file's details and frame its content on
/// stdout, skipping <c>someothertext.txt</c>. A failing <c>curl_easy_init</c> answers
/// <see cref="CurlCode.OutOfMemory"/>; otherwise the program answers the transfer's code.
/// </summary>
public sealed class Lib576 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib576";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = CurlCode.OutOfMemory;
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            var printer = new ChunkPrinter(context.StandardOutput);
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Wildcardmatch, 1L),
                    (CurlOption.ChunkBgnFunction, (ChunkBeginCallback)printer.Begin),
                    (CurlOption.ChunkEndFunction, (ChunkEndCallback)printer.End),
                    (CurlOption.ChunkData, printer),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>chunk_bgn</c> and <c>chunk_end</c> over <c>struct chunk_data</c>, printing with <c>curl_mprintf</c>.</summary>
    /// <param name="output">The program's stdout.</param>
    public sealed class ChunkPrinter(Stream output)
    {
        private const string Rule = "=============================================================\n";
        private const string ContentRule = "-------------------------------------------------------------\n";
        private int remains;
        private bool printContent;

        /// <summary><c>chunk_bgn</c>: prints the file's details, and opens its content when it is a regular file.</summary>
        /// <param name="file">The file about to be transferred.</param>
        /// <param name="remainingFiles">The files left, this one included.</param>
        /// <returns><see cref="ChunkBeginResult.Skip"/> for <c>someothertext.txt</c>; otherwise <see cref="ChunkBeginResult.Ok"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
        public ChunkBeginResult Begin(CurlFileInfo file, int remainingFiles)
        {
            ArgumentNullException.ThrowIfNull(file);
            remains = remainingFiles;
            var text = new StringBuilder();
            text.Append(Rule).Append(CultureInfo.InvariantCulture, $"Remains:      {remainingFiles}\n").Append(CultureInfo.InvariantCulture, $"Filename:     {file.FileName}\n");
            AppendDetails(text, file);
            text.Append("Filetype:     ").Append(FileTypeText(file));
            if (file.FileType == CurlFileType.File)
            {
                printContent = true;
                text.Append("Content:\n").Append(ContentRule);
            }

            bool skip = file.FileName == "someothertext.txt";
            if (skip)
            {
                text.Append("# THIS CONTENT WAS SKIPPED IN CHUNK_BGN CALLBACK #\n");
            }

            Print(text.ToString());
            return skip ? ChunkBeginResult.Skip : ChunkBeginResult.Ok;
        }

        /// <summary><c>chunk_end</c>: closes a file's content, and the listing after its last file.</summary>
        /// <returns><see cref="ChunkEndResult.Ok"/>.</returns>
        public ChunkEndResult End()
        {
            if (printContent)
            {
                printContent = false;
                Print(ContentRule);
            }

            if (remains == 1)
            {
                Print(Rule);
            }

            return ChunkEndResult.Ok;
        }

        private static void AppendDetails(StringBuilder text, CurlFileInfo file)
        {
            if (file.PermissionsText is { } permissions)
            {
                text.Append(CultureInfo.InvariantCulture, $"Permissions:  {permissions}");
                if (file.Permissions is { } parsed)
                {
                    text.Append(CultureInfo.InvariantCulture, $" (parsed => {Convert.ToString(parsed, 8)})");
                }

                text.Append('\n');
            }

            text.Append(CultureInfo.InvariantCulture, $"Size:         {file.Size ?? 0}B\n");
            AppendIfKnown(text, "User:         ", file.User);
            AppendIfKnown(text, "Group:        ", file.Group);
            AppendIfKnown(text, "Time:         ", file.TimeText);
        }

        private static void AppendIfKnown(StringBuilder text, string label, string? value)
        {
            if (value is not null)
            {
                text.Append(label).Append(value).Append('\n');
            }
        }

        private static string FileTypeText(CurlFileInfo file) => file.FileType switch
        {
            CurlFileType.File => "regular file\n",
            CurlFileType.Directory => "directory\n",
            CurlFileType.SymbolicLink => $"symlink\nTarget:       {file.Target ?? "(nil)"}\n",
            _ => "other type\n",
        };

        private void Print(string text) => output.Write(Encoding.UTF8.GetBytes(text));
    }
}
