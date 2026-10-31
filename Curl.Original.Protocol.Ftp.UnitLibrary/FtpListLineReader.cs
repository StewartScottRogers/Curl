using System.Runtime.InteropServices;
using System.Text;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Reads one file entry of an FTP <c>LIST</c> reply a byte at a time, as one format of upstream
/// curl 8.22.0's <c>lib/ftplistparser.c</c> state machine does. <see cref="Line" /> holds the
/// entry's bytes (curl's <c>infop-&gt;buf</c>); the owner appends each byte before
/// <see cref="Read" /> sees it.
/// </summary>
internal abstract class FtpListLineReader
{
    /// <summary>Where the item being read starts in <see cref="Line" /> (curl's <c>item_offset</c>).</summary>
    protected int ItemOffset { get; set; }

    /// <summary>How many bytes of the item have been read (curl's <c>item_length</c>).</summary>
    protected int ItemLength { get; set; }

    /// <summary>The step within the field being read (curl's sub-state); 0 is before the field.</summary>
    protected int Step { get; set; }

    /// <summary>The bytes of the entry read so far.</summary>
    public List<byte> Line { get; } = [];

    /// <summary>Starts a new entry: an empty line, as curl's fresh <c>struct fileinfo</c> is.</summary>
    public virtual void StartEntry()
    {
        Line.Clear();
        ItemOffset = 0;
        ItemLength = 0;
    }

    /// <summary>Reads the byte just appended to <see cref="Line" />.</summary>
    /// <param name="c">The byte.</param>
    /// <returns>What the byte did to the entry.</returns>
    public abstract FtpListStep Read(byte c);

    /// <summary>The entry the last <see cref="FtpListStep.EntryComplete" /> ended.</summary>
    /// <returns>The entry.</returns>
    public abstract FtpFileListEntry CompletedEntry();

    /// <summary>Starts an item at the byte just appended, at step 1.</summary>
    protected void BeginItem()
    {
        ItemOffset = Line.Count - 1;
        ItemLength = 1;
        Step = 1;
    }

    /// <summary>
    /// The bytes of <see cref="Line" /> from <paramref name="start" />, one character per byte,
    /// cut at the first NUL byte as a C string is.
    /// </summary>
    /// <param name="start">The first byte.</param>
    /// <param name="length">How many bytes at most.</param>
    /// <returns>The text.</returns>
    protected string Text(int start, int length)
    {
        string text = Encoding.Latin1.GetString(CollectionsMarshal.AsSpan(Line).Slice(start, length));
        int nul = text.IndexOf('\0', StringComparison.Ordinal);
        return nul < 0 ? text : text[..nul];
    }

    /// <summary>
    /// Reads a field that runs to the end of the line, ended by LF or by CR LF, as curl's file
    /// name and link target states do; <see cref="Step" /> moves one on past a CR.
    /// </summary>
    /// <param name="c">The byte.</param>
    /// <param name="afterCarriageReturn">Whether the previous byte was the field's CR.</param>
    /// <param name="field">The field, without its line end, once the line ends.</param>
    /// <returns><see cref="FtpListStep.EntryComplete" /> at the line end, <see cref="FtpListStep.Malformed" /> for a CR not followed by LF.</returns>
    protected FtpListStep ReadToLineEnd(byte c, bool afterCarriageReturn, out string? field)
    {
        field = null;
        if (!afterCarriageReturn)
        {
            ItemLength++;
            if (c == '\r')
            {
                Step++;
                return FtpListStep.Continue;
            }
        }

        if (c != '\n')
        {
            return afterCarriageReturn ? FtpListStep.Malformed : FtpListStep.Continue;
        }

        field = Text(ItemOffset, ItemLength - 1);
        return FtpListStep.EntryComplete;
    }

    /// <summary>Whether <paramref name="c" /> is an ASCII digit, curl's <c>ISDIGIT</c>.</summary>
    /// <param name="c">The byte.</param>
    /// <returns><see langword="true" /> for <c>0</c> to <c>9</c>.</returns>
    protected static bool IsDigit(byte c) => char.IsAsciiDigit((char)c);

    /// <summary>
    /// Reads the decimal number at the start of <paramref name="text" /> as curl's
    /// <c>curlx_str_number</c> does: at least one digit, and no more than <paramref name="max" />.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The largest value allowed.</param>
    /// <returns>The number, or <see langword="null" /> with no leading digit or past <paramref name="max" />.</returns>
    protected static long? LeadingNumber(string text, long max)
    {
        if (text.Length == 0 || !char.IsAsciiDigit(text[0]))
        {
            return null;
        }

        long number = 0;
        foreach (char c in text.TakeWhile(char.IsAsciiDigit))
        {
            int digit = c - '0';
            if (number > (max - digit) / 10)
            {
                return null;
            }

            number = (number * 10) + digit;
        }

        return number;
    }
}
