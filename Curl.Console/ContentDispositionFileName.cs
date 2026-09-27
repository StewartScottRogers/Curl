using System.Buffers;
using System.Text;

namespace Curl.Console;

/// <summary>
/// Finds the file name <c>-J</c> / <c>--remote-header-name</c> takes from a
/// <c>Content-Disposition</c> header line, as curl 8.21.0's header callback does: the
/// <c>filename=</c> parameter only (<c>filename*=</c> is not supported), matched in lower
/// case, its value up to the closing quote or the next <c>;</c>, and only the part after
/// its last <c>/</c> or <c>\</c>.
/// </summary>
/// <remarks>
/// <para>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-239 Notes),
/// <c>curl -sS -OJ http://127.0.0.1:18239/u.txt</c> against a loopback server answering with
/// the header: <c>attachment; filename="x y.txt"</c> writes <c>x y.txt</c>;
/// <c>attachment; filename=plain.txt; size=5</c> writes <c>plain.txt</c>;
/// <c>attachment;filename=semi.txt;</c> writes <c>semi.txt</c>; <c>attachment; filename='sq.txt'</c>
/// writes <c>sq.txt</c>; <c>filename="../../dir/evil.txt"</c> writes <c>evil.txt</c>;
/// <c>filename="C:\x\win.txt"</c> writes <c>win.txt</c>. <c>filename*=UTF-8''star.txt</c>,
/// <c>FileName=Up.txt</c>, <c>inline</c> and <c>filename="dir/"</c> name nothing, so the URL's
/// name <c>u.txt</c> is written; <c>filename=""</c> names the empty file, which fails to open.
/// </para>
/// <para>
/// curl uses the value's bytes as the name. They are read here as UTF-8, the encoding .NET
/// file names round-trip through on every platform.
/// </para>
/// </remarks>
internal static class ContentDispositionFileName
{
    /// <summary>The header name, which curl matches in any case.</summary>
    private const string HeaderPrefix = "Content-disposition:";

    /// <summary>The ASCII letters, where curl starts reading each parameter name.</summary>
    private static readonly SearchValues<byte> Letters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    /// <summary>The parameter curl looks for, matched exactly.</summary>
    private static ReadOnlySpan<byte> FileNameParameter => "filename="u8;

    /// <summary>
    /// Finds the file name in one header line.
    /// </summary>
    /// <param name="line">One header line, with or without its CR LF.</param>
    /// <returns>
    /// The name, which may be empty; <see langword="null" /> when the line is not a
    /// <c>Content-Disposition</c> header, has no <c>filename=</c> parameter, or its value
    /// ends in <c>/</c> or <c>\</c>.
    /// </returns>
    internal static string? Find(ReadOnlySpan<byte> line)
    {
        if (line.Length <= HeaderPrefix.Length
            || !Ascii.EqualsIgnoreCase(line[..HeaderPrefix.Length], HeaderPrefix))
        {
            return null;
        }

        ReadOnlySpan<byte> parameters = line[HeaderPrefix.Length..];
        int value = FileNameValueStart(parameters);

        return value < 0 ? null : ParseValue(parameters[value..]);
    }

    /// <summary>
    /// Finds where the <c>filename=</c> parameter's value starts, as curl does: from each
    /// parameter's first letter, skipping to the next <c>;</c> when it is another parameter.
    /// </summary>
    /// <param name="parameters">The header's value, after <c>Content-Disposition:</c>.</param>
    /// <returns>The index just after <c>filename=</c>, or -1 when there is no such parameter.</returns>
    private static int FileNameValueStart(ReadOnlySpan<byte> parameters)
    {
        int position = 0;
        while (true)
        {
            int letter = parameters[position..].IndexOfAny(Letters);
            if (letter < 0 || parameters.Length - position - letter < FileNameParameter.Length)
            {
                return -1;
            }

            position += letter;
            if (parameters[position..].StartsWith(FileNameParameter))
            {
                return position + FileNameParameter.Length;
            }

            int semicolon = parameters[position..].IndexOf((byte)';');
            if (semicolon < 0)
            {
                return -1;
            }

            position += semicolon;
        }
    }

    /// <summary>
    /// Reads a <c>filename=</c> value as curl's <c>parse_filename</c> does.
    /// </summary>
    /// <param name="value">The bytes after <c>filename=</c>, to the end of the line.</param>
    /// <returns>The name, or <see langword="null" /> when the value ends in <c>/</c> or <c>\</c>.</returns>
    private static string? ParseValue(ReadOnlySpan<byte> value)
    {
        bool quoted = StartsWithQuote(value);
        ReadOnlySpan<byte> name = quoted ? UpTo(value[1..], value[0]) : UpTo(value, (byte)';');
        int slash = name.LastIndexOfAny((byte)'/', (byte)'\\');
        if (slash >= 0 && slash == name.Length - 1)
        {
            return null;
        }

        return Encoding.UTF8.GetString(UpTo(UpTo(name[(slash + 1)..], (byte)'\r'), (byte)'\n'));
    }

    /// <summary>
    /// Tells whether a value starts with the <c>"</c> or <c>'</c> curl reads it up to.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true" /> when the value is quoted.</returns>
    private static bool StartsWithQuote(ReadOnlySpan<byte> value) =>
        !value.IsEmpty && value[0] is (byte)'"' or (byte)'\'';

    /// <summary>
    /// Cuts <paramref name="value" /> at the first <paramref name="stop" />.
    /// </summary>
    /// <param name="value">The bytes.</param>
    /// <param name="stop">The byte to stop at.</param>
    /// <returns>The bytes before the first <paramref name="stop" />, or all of them when there is none.</returns>
    private static ReadOnlySpan<byte> UpTo(ReadOnlySpan<byte> value, byte stop)
    {
        int index = value.IndexOf(stop);

        return index < 0 ? value : value[..index];
    }
}
