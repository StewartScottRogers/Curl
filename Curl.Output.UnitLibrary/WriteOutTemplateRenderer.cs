using System.Buffers;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Renders a <c>-w</c> / <c>--write-out</c> template after a transfer, as curl 8.21.0's
/// <c>tool_writeout.c</c> does: <c>%{name}</c> variables, <c>%header{name}</c> and
/// <c>%header{name:all:separator}</c> (every value joined, <c>\}</c> a literal brace),
/// <c>%output{file}</c> and <c>%output{&gt;&gt;file}</c>, <c>%{stdout}</c> and
/// <c>%{stderr}</c>, the <c>\n</c>, <c>\r</c> and <c>\t</c> escapes, and <c>%%</c>.
/// </summary>
/// <remarks>
/// <para>
/// Anything that is not one of those is written as it stands: <c>%x</c> stays <c>%x</c>,
/// <c>\\</c> stays <c>\\</c>, <c>\q</c> stays <c>\q</c>, and a <c>%{</c>,
/// <c>%header{</c> or <c>%output{</c> with no closing brace is written as that text and
/// rendering carries on after it. An unknown variable writes
/// <see cref="UnknownVariableWarning"/> to standard error and nothing to the output.
/// </para>
/// <para>
/// <c>%{onerror}</c> ends the rendering there when the transfer succeeded, and renders
/// nothing when it failed (<see cref="IWriteOutVariableSource.TransferFailed"/>).
/// <c>%time{format}</c> renders the current time through <see cref="WriteOutTimeFormatter"/>,
/// in the <see cref="WriteOutTimeDialect"/> the renderer was given.
/// </para>
/// <para>
/// A file that cannot be opened leaves the output where it was. A header name of 256 bytes
/// or more renders nothing, and a file name of 512 bytes or more is not opened, as curl's
/// fixed buffers do. A <c>%{name}</c> of 24 bytes or more ends the rendering there,
/// silently, as curl's 24-byte name buffer does. Text is written in the text encoding the
/// renderer was given (UTF-8, or on Windows the ANSI code page curl's arguments arrive in,
/// CGR-015, ADR-0830), except header
/// bytes marked by <see cref="MarkHeaderBytes"/>, which are written as the raw bytes they
/// were (CGR-021, ADR-0816), and lone surrogates U+DC80 to U+DCFF, which stand for argument
/// bytes that were not valid UTF-8 (CGR-016, ADR-0822), which are written as those bytes, as
/// curl writes its arguments unchanged.
/// </para>
/// <para>
/// The Windows curl writes all three targets in text mode, so every line feed it writes,
/// from the template or from a value, reaches the stream as CR LF; measured on 2026-09-26
/// against curl 8.21.0 (mingw, Schannel). Pass <c>writesLineFeedAsCrLf</c> to match it.
/// </para>
/// </remarks>
/// <param name="fileOpener">Opens the <c>%output{file}</c> targets.</param>
/// <param name="writesLineFeedAsCrLf"><see langword="true"/> to write each line feed as CR LF, as the Windows curl does.</param>
/// <param name="timeDialect">The C runtime whose <c>strftime</c> <c>%time{format}</c> follows: the Windows one, or glibc for Linux and macOS.</param>
/// <param name="timeProvider">Supplies the time <c>%time{format}</c> renders.</param>
/// <param name="textEncoding">The encoding text is written in: UTF-8, or the ANSI code page on Windows, as curl writes its argument bytes.</param>
public sealed class WriteOutTemplateRenderer(
    IWriteOutFileOpener fileOpener,
    bool writesLineFeedAsCrLf,
    WriteOutTimeDialect timeDialect,
    TimeProvider timeProvider,
    Encoding textEncoding)
{
    /// <summary>Creates a renderer that writes its text as UTF-8.</summary>
    /// <param name="fileOpener">Opens the <c>%output{file}</c> targets.</param>
    /// <param name="writesLineFeedAsCrLf"><see langword="true"/> to write each line feed as CR LF, as the Windows curl does.</param>
    /// <param name="timeDialect">The C runtime whose <c>strftime</c> <c>%time{format}</c> follows.</param>
    /// <param name="timeProvider">Supplies the time <c>%time{format}</c> renders.</param>
    public WriteOutTemplateRenderer(IWriteOutFileOpener fileOpener, bool writesLineFeedAsCrLf, WriteOutTimeDialect timeDialect, TimeProvider timeProvider)
        : this(fileOpener, writesLineFeedAsCrLf, timeDialect, timeProvider, Encoding.UTF8)
    {
    }

    private const int VariableNameBufferBytes = 24;
    private const int HeaderNameBufferBytes = 256;
    private const int FileNameBufferBytes = 512;
    private const char FirstHighByte = '\u0080';
    private const char LastHighByte = 'ÿ';
    private const int RawByteOffset = 0xF700;
    private const char FirstInvalidByteEscape = '\uDC80';
    private const char LastInvalidByteEscape = '\uDCFF';

    /// <summary>
    /// The characters written as one raw byte each: a marked header byte (U+F780 to U+F7FF)
    /// and an invalid UTF-8 argument byte kept as a lone surrogate (U+DC80 to U+DCFF, ADR-0822);
    /// byte <c>b</c> is the character's low eight bits either way.
    /// </summary>
    private static readonly SearchValues<char> RawByteCharacters = SearchValues.Create(
        CharactersFrom((char)(RawByteOffset + FirstHighByte), (char)(RawByteOffset + LastHighByte))
        + CharactersFrom(FirstInvalidByteEscape, LastInvalidByteEscape));

    private readonly IWriteOutFileOpener fileOpener = fileOpener ?? throw new ArgumentNullException(nameof(fileOpener));
    private readonly WriteOutTimeDialect timeDialect = timeDialect;
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly Encoding textEncoding = textEncoding ?? throw new ArgumentNullException(nameof(textEncoding));

    /// <summary>
    /// The warning line, without its line terminator, curl writes to standard error for a
    /// <c>%{name}</c> it does not know: <c>curl: unknown --write-out variable: '&lt;name&gt;'</c>.
    /// </summary>
    /// <param name="name">The name exactly as written between the braces.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public static string UnknownVariableWarning(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return $"curl: unknown --write-out variable: '{name}'";
    }

    /// <summary>
    /// Renders <paramref name="template"/>, starting on standard output. Files opened by
    /// <c>%output{…}</c> are disposed before this returns; the two standard streams are not.
    /// </summary>
    /// <param name="template">The template as given to <c>-w</c>.</param>
    /// <param name="variables">The finished transfer's variables and response headers.</param>
    /// <param name="standardOutput">Standard output.</param>
    /// <param name="standardError">Standard error, which also receives the unknown-variable warnings.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the template is rendered.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public async Task RenderAsync(
        string template,
        IWriteOutVariableSource variables,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        Rendering rendering = new(this, template, variables, standardOutput, standardError, cancellationToken);
        await using (rendering.ConfigureAwait(false))
        {
            await rendering.RenderAllAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Marks each character from U+0080 to U+00FF of <paramref name="headerText"/>, a header
    /// text whose bytes are held one per character (Latin-1), so that
    /// <see cref="RenderAsync"/> writes it back as that one byte rather than as UTF-8, as curl
    /// writes header bytes unchanged. Every other character is kept as it is.
    /// </summary>
    /// <remarks>
    /// A marked byte is carried as a character from U+F780 to U+F7FF, byte <c>b</c> as
    /// <c>U+F700 + b</c>; a template that itself holds one of those private-use characters
    /// therefore writes it as that raw byte.
    /// </remarks>
    /// <param name="headerText">The header text, one byte per character.</param>
    /// <returns>The text with its bytes from 0x80 marked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerText"/> is <see langword="null"/>.</exception>
    public static string MarkHeaderBytes(string headerText)
    {
        ArgumentNullException.ThrowIfNull(headerText);
        return string.Create(headerText.Length, headerText, static (marked, text) =>
        {
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                marked[index] = character is >= FirstHighByte and <= LastHighByte ? (char)(RawByteOffset + character) : character;
            }
        });
    }

    private byte[] Encode(string text)
    {
        string lines = writesLineFeedAsCrLf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
        return lines.AsSpan().IndexOfAny(RawByteCharacters) < 0
            ? textEncoding.GetBytes(lines)
            : EncodeWithRawBytes(lines);
    }

    private static string CharactersFrom(char first, char last) =>
        string.Create(last - first + 1, first, static (characters, start) =>
        {
            for (int index = 0; index < characters.Length; index++)
            {
                characters[index] = (char)(start + index);
            }
        });

    private byte[] EncodeWithRawBytes(string text)
    {
        List<byte> bytes = new(text.Length);
        StringBuilder textRun = new();
        ReadOnlySpan<char> rest = text;
        while (!rest.IsEmpty)
        {
            Rune.DecodeFromUtf16(rest, out _, out int consumed);
            char first = rest[0];
            if (RawByteCharacters.Contains(first))
            {
                AppendEncodedRun(bytes, textRun);
                bytes.Add((byte)first);
            }
            else
            {
                textRun.Append(rest[..consumed]);
            }

            rest = rest[consumed..];
        }

        AppendEncodedRun(bytes, textRun);
        return [.. bytes];
    }

    private void AppendEncodedRun(List<byte> bytes, StringBuilder textRun)
    {
        bytes.AddRange(textEncoding.GetBytes(textRun.ToString()));
        textRun.Clear();
    }

    /// <summary>One pass over one template, holding where the output currently goes.</summary>
    private sealed class Rendering(
        WriteOutTemplateRenderer renderer,
        string template,
        IWriteOutVariableSource variables,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken) : IAsyncDisposable
    {
        private const string VariableOpening = "%{";
        private const string HeaderOpening = "%header{";
        private const string OutputOpening = "%output{";
        private const string TimeOpening = "%time{";
        private const string AppendMarker = ">>";
        private const string AllHeaderValuesMarker = ":all:";

        private readonly StringBuilder pending = new();
        private Stream target = Stream.Null;
        private Stream? openedFile;
        private int position;

        public async Task RenderAllAsync()
        {
            target = standardOutput;
            while (position < template.Length)
            {
                await RenderNextAsync().ConfigureAwait(false);
            }

            await FlushAsync().ConfigureAwait(false);
        }

        /// <summary>Closes the file <c>%output{…}</c> left open, if any.</summary>
        public ValueTask DisposeAsync()
        {
            return new ValueTask(CloseOpenedFileAsync());
        }

        private async Task CloseOpenedFileAsync()
        {
            if (openedFile is not null)
            {
                await openedFile.DisposeAsync().ConfigureAwait(false);
                openedFile = null;
            }
        }

        private async Task RenderNextAsync()
        {
            bool hasFollowing = position + 1 < template.Length;
            char current = template[position];
            if (current == '%' && hasFollowing)
            {
                await RenderPercentAsync().ConfigureAwait(false);
            }
            else if (current == '\\' && hasFollowing)
            {
                RenderEscape();
            }
            else
            {
                pending.Append(current);
                position++;
            }
        }

        private async Task RenderPercentAsync()
        {
            if (template[position + 1] == '%')
            {
                pending.Append('%');
                position += 2;
            }
            else if (IsAt(VariableOpening))
            {
                await RenderVariableAsync().ConfigureAwait(false);
            }
            else if (IsAt(HeaderOpening))
            {
                RenderHeader();
            }
            else if (IsAt(OutputOpening))
            {
                await RenderOutputAsync().ConfigureAwait(false);
            }
            else if (IsAt(TimeOpening))
            {
                RenderTime();
            }
            else
            {
                pending.Append(template, position, 2);
                position += 2;
            }
        }

        private void RenderEscape()
        {
            char escaped = template[position + 1];
            switch (escaped)
            {
                case 'n':
                    pending.Append('\n');
                    break;
                case 'r':
                    pending.Append('\r');
                    break;
                case 't':
                    pending.Append('\t');
                    break;
                default:
                    pending.Append('\\').Append(escaped);
                    break;
            }

            position += 2;
        }

        private async Task RenderVariableAsync()
        {
            position += VariableOpening.Length;
            if (!TryReadToClosingBrace(out string name))
            {
                pending.Append(VariableOpening);
                return;
            }

            // curl's name buffer refuses a name this long, and its loop ends there silently.
            if (renderer.textEncoding.GetByteCount(name) >= VariableNameBufferBytes)
            {
                position = template.Length;
                return;
            }

            switch (name)
            {
                case "stdout":
                    await SwitchTargetAsync(standardOutput, null).ConfigureAwait(false);
                    break;
                case "stderr":
                    await SwitchTargetAsync(standardError, null).ConfigureAwait(false);
                    break;
                case "onerror":
                    StopUnlessTransferFailed();
                    break;
                default:
                    await RenderTransferVariableAsync(name).ConfigureAwait(false);
                    break;
            }
        }

        private async Task RenderTransferVariableAsync(string name)
        {
            if (variables.TryGetVariableText(name, out string? text))
            {
                pending.Append(text);
                return;
            }

            await FlushAsync().ConfigureAwait(false);
            await standardError.WriteAsync(renderer.Encode(UnknownVariableWarning(name) + "\n"), cancellationToken).ConfigureAwait(false);
        }

        private void StopUnlessTransferFailed()
        {
            if (!variables.TransferFailed)
            {
                position = template.Length;
            }
        }

        private void RenderTime()
        {
            position += TimeOpening.Length;
            if (!TryReadToClosingBrace(out string format))
            {
                pending.Append(TimeOpening);
                return;
            }

            pending.Append(WriteOutTimeFormatter.Format(format, renderer.timeDialect, renderer.timeProvider));
        }

        private void RenderHeader()
        {
            position += HeaderOpening.Length;
            if (!TryReadToUnescapedClosingBrace(out string content))
            {
                pending.Append(HeaderOpening);
                return;
            }

            if (renderer.textEncoding.GetByteCount(content) >= HeaderNameBufferBytes)
            {
                return;
            }

            int allMarker = content.IndexOf(AllHeaderValuesMarker, StringComparison.Ordinal);
            if (allMarker < 0)
            {
                pending.Append(variables.FindFirstHeaderValue(content));
                return;
            }

            string separator = content[(allMarker + AllHeaderValuesMarker.Length)..].Replace("\\}", "}", StringComparison.Ordinal);
            pending.AppendJoin(separator, variables.FindAllHeaderValues(content[..allMarker]));
        }

        /// <summary>
        /// Reads to the first <c>}</c> not escaped as <c>\}</c>, as curl's <c>%header{</c> does.
        /// </summary>
        private bool TryReadToUnescapedClosingBrace(out string content)
        {
            int closingBrace = template.IndexOf('}', position);
            while (closingBrace > position && template[closingBrace - 1] == '\\')
            {
                closingBrace = template.IndexOf('}', closingBrace + 1);
            }

            if (closingBrace < 0)
            {
                content = string.Empty;
                return false;
            }

            content = template[position..closingBrace];
            position = closingBrace + 1;
            return true;
        }

        private async Task RenderOutputAsync()
        {
            position += OutputOpening.Length;
            bool append = IsAt(AppendMarker);
            if (append)
            {
                position += AppendMarker.Length;
            }

            if (!TryReadToClosingBrace(out string path))
            {
                pending.Append(OutputOpening);
                return;
            }

            if (renderer.textEncoding.GetByteCount(path) >= FileNameBufferBytes)
            {
                return;
            }

            // Everything written so far reaches the current file before the next one opens,
            // so a %output{>>same-file} appends after it, as curl's C streams do.
            await FlushAsync().ConfigureAwait(false);
            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (renderer.fileOpener.TryOpen(path, append, out Stream? file))
            {
                await SwitchTargetAsync(file, file).ConfigureAwait(false);
            }
        }

        private async Task SwitchTargetAsync(Stream newTarget, Stream? newOpenedFile)
        {
            await FlushAsync().ConfigureAwait(false);

            // Take the new file on before closing the old one, so a failing close still
            // leaves the new file where DisposeAsync closes it.
            Stream? previousFile = openedFile;
            target = newTarget;
            openedFile = newOpenedFile;
            if (previousFile is not null)
            {
                await previousFile.DisposeAsync().ConfigureAwait(false);
            }
        }

        private bool IsAt(string text)
        {
            return template.AsSpan(position).StartsWith(text, StringComparison.Ordinal);
        }

        private bool TryReadToClosingBrace(out string content)
        {
            int closingBrace = template.IndexOf('}', position);
            if (closingBrace < 0)
            {
                content = string.Empty;
                return false;
            }

            content = template[position..closingBrace];
            position = closingBrace + 1;
            return true;
        }

        private async Task FlushAsync()
        {
            if (pending.Length > 0)
            {
                await target.WriteAsync(renderer.Encode(pending.ToString()), cancellationToken).ConfigureAwait(false);
                pending.Clear();
            }
        }
    }
}
